import os
from collections import defaultdict
from typing import List

from src.core.models import ImageGroup


def group_images(image_paths: List[str]) -> List[ImageGroup]:
    """Groups image paths based on their base filenames."""
    groups = defaultdict(list)
    for path in image_paths:
        filename = os.path.basename(path)
        name_part, _ = os.path.splitext(filename)
        if '_' in name_part:
            base_name = name_part.rsplit('_', 1)[0]
        else:
            base_name = name_part
        groups[base_name].append(path)

    image_groups = []
    for base_name, paths in groups.items():
        paths.sort()
        image_groups.append(ImageGroup(group_id=base_name, image_paths=paths[:2]))

    return image_groups
