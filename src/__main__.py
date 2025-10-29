import logging
import os
import site
import sys

# Smart path handling for both script execution and PyInstaller bundles
if getattr(sys, 'frozen', False):
    application_path = sys._MEIPASS
    if application_path not in sys.path:
        sys.path.insert(0, application_path)
    # Set HF_HOME to point to the bundled models directory
    os.environ['HF_HOME'] = os.path.join(application_path, 'models')
    logging.info(f"HF_HOME set to: {os.environ['HF_HOME']}")

    # Debugging path resolution
    import rapidocr
    logging.info(f"sys._MEIPASS: {sys._MEIPASS}")
    logging.info(f"rapidocr.__file__: {rapidocr.__file__}")
    logging.info(f"os.path.dirname(rapidocr.__file__): {os.path.dirname(rapidocr.__file__)}")
    # Assuming rapidocr is directly under _MEIPASS
    rapidocr_root_in_bundle = os.path.abspath(os.path.join(os.path.dirname(rapidocr.__file__), '..', '..'))
    logging.info(f"rapidocr_root_in_bundle: {rapidocr_root_in_bundle}")
    logging.info(f"Current Working Directory: {os.getcwd()}")
else:
    # Running as a normal .py script, so find the project root
    project_root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
    if project_root not in sys.path:
        sys.path.insert(0, project_root)

from src.app.main_window import MainWindow
from PySide6.QtWidgets import QApplication

# Configure logging for the entire application (more robust)

# Clear existing handlers from the root logger
for handler in logging.getLogger().handlers[:]:
    logging.getLogger().removeHandler(handler)

logging.getLogger().setLevel(logging.INFO) # Set root logger level

# Explicitly set stdout/stderr encoding to UTF-8
if sys.stdout is not None and sys.stdout.encoding != 'utf-8':
    sys.stdout.reconfigure(encoding='utf-8')
if sys.stderr is not None and sys.stderr.encoding != 'utf-8':
    sys.stderr.reconfigure(encoding='utf-8')

def main():
    app = QApplication(sys.argv)
    window = MainWindow()
    window.show()
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
