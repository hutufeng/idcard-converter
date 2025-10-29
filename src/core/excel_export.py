import openpyxl

from PySide6.QtWidgets import QTableView

from src.core.models import AppState

def export_to_excel(app_state: AppState, file_path: str, table_view: QTableView):
    """Exports the data from AppState to an Excel .xlsx file, only including visible columns from the QTableView."""
    workbook = openpyxl.Workbook()
    sheet = workbook.active
    sheet.title = "ID Card Records"

    # Collect visible column headers and their corresponding keys
    visible_headers = []
    visible_column_keys = []
    
    for i, column_key in enumerate(app_state.column_settings['order']):
        if not table_view.isColumnHidden(i):
            header = app_state.column_settings['custom_names'].get(
                column_key, column_key.replace('_', ' ').title()
            )
            visible_headers.append(header)
            visible_column_keys.append(column_key)

    # Write visible headers
    sheet.append(visible_headers)

    # Write data rows for visible columns only
    for record in app_state.records:
        row_data = []
        for column_key in visible_column_keys:
            row_data.append(getattr(record, column_key, ""))
        sheet.append(row_data)

    workbook.save(file_path)
