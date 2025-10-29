from dataclasses import dataclass, field
from typing import Dict, List


@dataclass
class ImageGroup:
    """Represents a group of images for a single ID card, used for OCR processing."""
    group_id: str
    image_paths: List[str]
    status: str = "PENDING"  # PENDING, PROCESSING, COMPLETED, FAILED

    def __post_init__(self):
        if len(self.image_paths) > 2:
            raise ValueError("ImageGroup can have at most 2 image paths.")

@dataclass
class IDCardRecord:
    """Represents a single, complete ID card record extracted
    or calculated from images.
    """
    record_id: str
    name: str = ""
    gender: str = ""
    age: int = 0
    birth_date: str = ""
    ethnicity: str = ""
    id_number: str = ""
    address: str = ""
    issuing_authority: str = ""
    validity_period: str = ""
    source_images: List[str] = field(default_factory=list)
    status: str = "SUCCESS"  # SUCCESS, FAILED
    raw_ocr_output: str = "" # New field to store raw OCR output

@dataclass
class AppState:
    """Represents the application's UI state and user configuration."""
    records: List[IDCardRecord] = field(default_factory=list)
    column_settings: Dict = field(default_factory=lambda: {
        "order": [
            "record_id", "name", "gender", "age", "birth_date",
            "ethnicity", "id_number", "address",
            "issuing_authority", "validity_period", "status", "raw_ocr_output"
        ],
        "custom_names": {
            "record_id": "记录ID",
            "name": "姓名",
            "gender": "性别",
            "age": "年龄",
            "birth_date": "出生日期",
            "ethnicity": "民族",
            "id_number": "身份证号码",
            "address": "地址",
            "issuing_authority": "签发机关",
            "validity_period": "有效期",
            "status": "状态",
            "raw_ocr_output": "原始OCR输出"
        }
    })
