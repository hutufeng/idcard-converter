
def fix_garbled_text(text: str) -> str:
    """
    Attempts to fix garbled Chinese text that occurs from encoding mismatches.
    This commonly happens when a string is decoded using the wrong codec.
    This function tries to reverse the process and decode with a more appropriate one.
    """
    try:
        # The mojibake pattern often involves latin-1 or cp1252 decoding
        # of GBK/UTF-8 bytes.
        # We encode it back to bytes and then decode with a common Chinese encoding.
        return text.encode('latin-1').decode('gbk')
    except (UnicodeEncodeError, UnicodeDecodeError):
        # If any error occurs, it means our assumption was wrong,
        # and the text was likely not garbled in this specific way.
        return text

