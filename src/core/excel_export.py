import openpyxl

from src.core.models import AppState


def export_to_excel(app_state: AppState, file_path: str):
    """Exports the data from AppState to an Excel .xlsx file."""
    workbook = openpyxl.Workbook()
    sheet = workbook.active
    sheet.title = "ID Card Records"

    # Write headers
    headers = []
    for column_key in app_state.column_settings['order']:
        header = app_state.column_settings['custom_names'].get(
            column_key, column_key.replace('_', ' ').title()
        )
        headers.append(header)
    sheet.append(headers)

    # Write data rows
    for record in app_state.records:
        row_data = []
        for column_key in app_state.column_settings['order']:
            row_data.append(getattr(record, column_key, ""))
        sheet.append(row_data)

    workbook.save(file_path)
