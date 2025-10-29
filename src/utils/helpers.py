import re
from datetime import datetime
from typing import List, Optional, Tuple


def get_info_from_id_number(id_number: str, current_year: int) -> tuple[str, str, int]:
    """Parses an 18-digit or 15-digit ID number to extract
    birth date, gender, and age."""
    if len(id_number) == 18:
        # Checksum validation for 18-digit ID
        factors = [7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2]
        checksum_map = '10X98765432'
        check_sum = sum(int(id_number[i]) * factors[i] for i in range(17)) % 11
        if checksum_map[check_sum] != id_number[17].upper():
            raise ValueError("Invalid ID number checksum.")

        birth_year = int(id_number[6:10])
        birth_month = int(id_number[10:12])
        birth_day = int(id_number[12:14])
        gender_digit = int(id_number[16])
    elif len(id_number) == 15:
        birth_year = 1900 + int(id_number[6:8])
        birth_month = int(id_number[8:10])
        birth_day = int(id_number[10:12])
        gender_digit = int(id_number[14])
    else:
        raise ValueError("ID number must be 15 or 18 digits long.")

    birth_date = f"{birth_year:04d}-{birth_month:02d}-{birth_day:02d}"
    gender = "男" if gender_digit % 2 != 0 else "女"
    age = current_year - birth_year - (
        (birth_month, birth_day) > (datetime.now().month, datetime.now().day)
    )

    return birth_date, gender, age

def parse_validity_period(text: str) -> Optional[str]:
    """Parses the validity period from OCR text, handling "长期" (long-term)
    and intelligently formatting messy date strings.
    """
    # 1. Broadly find the text block after the keyword.
    match = re.search(r"(?:有效|效期|期限)\s*(.+)", text)
    if not match:
        return None

    raw_str = match.group(1).strip()

    # 2. Check for "长期" (long-term).
    is_long_term = "长期" in raw_str

    # 3. Clean the string, keeping only digits.
    digits = re.sub(r"[^\d]", "", raw_str)

    # 4. Format and return based on the extracted digits.
    if is_long_term:
        if len(digits) >= 8:
            start_date = digits[:8]
            return f"{start_date[:4]}.{start_date[4:6]}.{start_date[6:8]}-长期"
    elif len(digits) >= 16:  # Should be 16 for a full start and end date.
        start_date = digits[:8]
        end_date = digits[8:16]
        return (
            f"{start_date[:4]}.{start_date[4:6]}.{start_date[6:8]}-"
            f"{end_date[:4]}.{end_date[4:6]}.{end_date[6:8]}"
        )

    # 5. Fallback for already well-formatted strings that might have been missed.
    if re.match(r"(\d{4}\.\d{2}\.\d{2})-(\d{4}\.\d{2}\.\d{2}|长期)", raw_str):
        return raw_str

    return None
