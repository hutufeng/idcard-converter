from PySide6.QtCore import QAbstractTableModel, Qt
from PySide6.QtGui import QColor

from src.core.models import AppState


class RecordTableModel(QAbstractTableModel):
    """A model to interface the AppState with a QTableView."""
    def __init__(self, app_state: AppState, parent=None):
        super().__init__(parent)
        self.app_state = app_state

    def rowCount(self, parent=None):
        return len(self.app_state.records)

    def columnCount(self, parent=None):
        return len(self.app_state.column_settings['order'])

    def data(self, index, role=Qt.DisplayRole):
        if not index.isValid():
            return None

        record = self.app_state.records[index.row()]

        if role == Qt.DisplayRole or role == Qt.EditRole:
            column_key = self.app_state.column_settings['order'][index.column()]
            return getattr(record, column_key, "")

        if role == Qt.BackgroundRole:
            if record.status == "FAILED":
                return QColor('red')
            if record.status == "PARTIAL":
                return QColor('yellow')

        return None

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        if role == Qt.DisplayRole and orientation == Qt.Horizontal:
            column_key = self.app_state.column_settings['order'][section]
            return self.app_state.column_settings['custom_names'].get(
                column_key, column_key.replace('_', ' ').title()
            )

        return None

    def flags(self, index):
        """Return the item flags for the given index."""
        return super().flags(index) | Qt.ItemIsEditable

    def setData(self, index, value, role=Qt.EditRole):
        """Set the data for the given index."""
        if role == Qt.EditRole:
            if not index.isValid():
                return False

            record = self.app_state.records[index.row()]
            column_key = self.app_state.column_settings['order'][index.column()]

            # Prevent editing of certain fields
            if column_key in ["record_id", "source_images", "raw_ocr_output"]:
                return False

            # Try to convert to the correct type
            try:
                original_value = getattr(record, column_key, "")
                if isinstance(original_value, int):
                    value = int(value)
                elif isinstance(original_value, float):
                    value = float(value)
            except (ValueError, TypeError):
                return False # Reject type conversion errors

            setattr(record, column_key, value)
            self.dataChanged.emit(index, index, [role])
            return True
        return False

    def update_data(self, new_app_state: AppState):
        """Inform the view that the model is about to change."""
        self.beginResetModel()
        self.app_state = new_app_state
        self.endResetModel()
