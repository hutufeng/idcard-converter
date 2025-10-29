import logging
import re
from datetime import datetime
from typing import List, Optional

from rapidocr import RapidOCR

from src.core.models import IDCardRecord
from src.utils.encoding_fix import fix_garbled_text
from src.utils.helpers import get_info_from_id_number, parse_validity_period

# --- RapidOCR Engine Instance (Singleton) ---
_rapidocr_engine = None

def get_rapidocr_engine() -> RapidOCR:
    global _rapidocr_engine
    if _rapidocr_engine is None:
        logging.info("Initializing RapidOCR engine...")
        _rapidocr_engine = RapidOCR()
        logging.info("RapidOCR engine initialized successfully.")
    return _rapidocr_engine


def ocr_image(image_path: str) -> Optional[List[List]]:
    """
    Processes an image using RapidOCR and returns its raw output.
    RapidOCR output format: list[list[bbox, text, confidence]]
    """
    logging.info(f"Processing image with RapidOCR: {image_path}")
    engine = get_rapidocr_engine()
    try:
            result = engine(image_path) # result is RapidOCROutput object
            logging.debug(f"Type of RapidOCR result: {type(result)}")
            logging.debug(f"Dir of RapidOCR result: {dir(result)}")

            if result:
                return result # Return the RapidOCROutput object itself for now
            else:
                logging.warning(f"No OCR results found for {image_path}.")
                return None
    except Exception as e:
        logging.error(
            f"Error during RapidOCR processing for {image_path}: {e}", exc_info=True
        )
        return None


def extract_info(
    ocr_results: object, record: Optional[IDCardRecord] = None
) -> IDCardRecord:
    """
    Extracts structured ID card information from raw RapidOCR results,
    optionally updating an existing record.
    """
    if record is None:
        record = IDCardRecord(record_id="temp_id")

    if not ocr_results:
        logging.warning("OCR results object is None.")
        record.status = "FAILED"
        return record

    if not hasattr(ocr_results, 'boxes') or not hasattr(ocr_results, 'txts') or not ocr_results.txts:
        logging.warning("OCR result object is invalid or empty.")
        record.status = "FAILED"
        return record

    # Reconstruct the list of [bbox, text, confidence]
    results_list = []
    for box, text, score in zip(ocr_results.boxes, ocr_results.txts, ocr_results.scores):
        results_list.append([box, text, score])

    # Attempt to fix garbled text for each item.
    fixed_results = []
    for item in results_list:
        text = item[1]
        if text:
            text = fix_garbled_text(text)
        fixed_results.append([item[0], text, item[2]])

    # --- Final Unified Extraction Logic ---

    # 1. Find ID number first, as it's the most reliable field.
    full_text_corpus = " ".join([item[1] for item in fixed_results if item[1]])
    if not record.id_number:
        id_match = re.search(r'\d{17}[\dXx]', full_text_corpus.replace(" ", ""))
        if id_match:
            record.id_number = id_match.group(0).upper()
            try:
                current_year = datetime.now().year
                birth_date, gender, age = get_info_from_id_number(
                    record.id_number, current_year
                )
                record.birth_date = birth_date
                record.gender = gender
                record.age = age
                logging.debug(f"Derived info from ID: {record.id_number}")
            except ValueError as e:
                logging.warning(f"Could not parse ID number {record.id_number}: {e}")

    # 2. Unified field extraction with refined keyword matching and greedy value extraction.
    ALL_KEYWORDS = ["姓名", "民族", "住址", "公民身份号码", "签发机关", "有效期限"]

    for i, item in enumerate(fixed_results):
        text = item[1]
        if not text:
            continue

        def get_greedy_value(start_line_index: int, keyword_to_remove: str) -> str:
            """Extracts text starting from a keyword until the next keyword is found."""
            first_line = fixed_results[start_line_index][1]
            
            # Use regex to remove keyword only from the start of the string
            cleaned_first_line = re.sub(f'^{keyword_to_remove}', '', first_line).strip()
            
            value_parts = [cleaned_first_line]
            
            # Look ahead to subsequent lines
            for j in range(start_line_index + 1, len(fixed_results)):
                next_text = fixed_results[j][1]
                if not next_text:
                    continue
                if any(kw in next_text for kw in ALL_KEYWORDS):
                    break
                value_parts.append(next_text.strip())
            
            return "".join(value_parts).strip()

        # --- Apply the unified extraction logic with refined matching ---
        # Exact matching for short keywords
        if not record.name and "姓名" in text:
            # Name is never multi-line, so use non-greedy logic.
            match = re.search(r"姓名(.+)", text)
            if match:
                name_part = match.group(1)
                # Clean up by stopping at other keywords on the same line
                if "性别" in name_part:
                    name_part = name_part.split("性别")[0]
                if "民族" in name_part:
                    name_part = name_part.split("民族")[0]
                # Clean any erroneous letters from the final value
                record.name = re.sub(r"[a-zA-Z]", "", name_part).strip()
                logging.debug(f"  Extracted Name: '{record.name}'")

        if not record.ethnicity and "民族" in text:
            # Ethnicity is also a single field on a line.
            match = re.search(r"民族(\S+)", text)
            if match:
                raw_ethnicity = match.group(1).strip()
                record.ethnicity = re.sub(r"[a-zA-Z]", "", raw_ethnicity)
                logging.debug(f"  Extracted Ethnicity: '{record.ethnicity}'")

        if not record.address and "住址" in text:
            # Address can contain letters, so no cleaning is applied.
            record.address = get_greedy_value(i, "住址")
            logging.debug(f"  Extracted Address: '{record.address}'")

        # Lenient matching for longer keywords
        if not record.issuing_authority and sum(1 for c in "签发机关" if c in text) >= 2:
            raw_authority = get_greedy_value(i, "签发机关")
            if raw_authority:
                record.issuing_authority = re.sub(r"[a-zA-Z]", "", raw_authority)
                logging.debug(f"  Extracted Issuing Authority: '{record.issuing_authority}'")

        if not record.validity_period and sum(1 for c in "有效期限" if c in text) >= 2:
            full_period_text = get_greedy_value(i, "有效期限")
            if full_period_text:
                period = parse_validity_period(f"有效期限{full_period_text}")
                if period:
                    record.validity_period = period
                    logging.debug(f"  Extracted Validity Period: '{record.validity_period}'")

    # 3. Final Status Assessment
    required_fields = [record.name, record.id_number, record.address, record.issuing_authority, record.validity_period]
    if all(required_fields):
        record.status = "SUCCESS"
    elif record.id_number: # If we have the ID, it's at least a partial success
        record.status = "PARTIAL"
    else:
        record.status = "FAILED"

    return record
