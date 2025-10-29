import os
import shutil
import platform

import PyInstaller.__main__

def build_app():
    print("Starting application build...")

    # Clean up previous build artifacts
    if os.path.exists('build'):
        shutil.rmtree('build')
    if os.path.exists('dist'):
        shutil.rmtree('dist')

    # Define the separator for add-data based on the OS
    separator = ';' if platform.system() == 'Windows' else ':'

    # Define the absolute path to default_models.yaml in the conda environment
    # This path is specific to the user's environment and needs to be accurate.
    rapidocr_models_yaml_path = "C:\\Users\\hutu_\\scoop\\apps\\miniconda3\\current\\envs\\idcard_ocr\\Lib\\site-packages\\rapidocr\\default_models.yaml"
    rapidocr_config_yaml_path = os.path.join(os.path.dirname(rapidocr_models_yaml_path), "config.yaml")

    # Run PyInstaller with corrected arguments
    PyInstaller.__main__.run([
        'src/__main__.py',
        '--name=IDCardOCRApp',
        '--onedir',  # Use one-folder mode for faster startup
        '--windowed',  # For GUI applications
        f'--add-data=models{separator}models',
        f'--add-data={rapidocr_models_yaml_path}{separator}rapidocr',
        f'--add-data={rapidocr_config_yaml_path}{separator}rapidocr',
        # Hidden imports can help PyInstaller find modules it might miss
        '--hidden-import=PySide6.QtCore',
        '--hidden-import=PySide6.QtGui',
        '--hidden-import=PySide6.QtWidgets',
        '--hidden-import=openpyxl',
        '--hidden-import=rapidocr',
    ])

    print("Build process finished.")
    print("You can find the packaged application in the 'dist/IDCardOCRApp' folder.")

if __name__ == '__main__':
    build_app()
