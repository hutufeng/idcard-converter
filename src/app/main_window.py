import logging
import os

from PySide6.QtCore import Qt, QThread, Signal, QSettings
from PySide6.QtGui import QAction
from PySide6.QtWidgets import (
    QApplication,
    QFileDialog,
    QHBoxLayout,
    QInputDialog,
    QListWidget,
    QMainWindow,
    QMenu,
    QProgressBar,
    QPushButton,
    QStatusBar,
    QTableView,
    QToolBar,
    QVBoxLayout,
    QWidget,
)

from .table_model import RecordTableModel
from ..core.excel_export import export_to_excel
from ..core.grouping import group_images
from ..core.models import AppState, IDCardRecord
from ..core.ocr import extract_info, ocr_image


class Worker(QThread):
    """Worker thread for long-running OCR tasks."""
    finished = Signal(object)
    progress = Signal(int)
    grouping_started = Signal(int)
    grouping_finished = Signal(int)
    ocr_started = Signal(str)
    ocr_finished = Signal(str, str)
    ocr_error = Signal(str, str)

    def __init__(self, image_paths):
        super().__init__()
        self.image_paths = image_paths
        self._is_stopped = False

    def stop(self):
        self._is_stopped = True

    def run(self):
        """Group images, perform OCR, and extract info."""
        app_state = AppState()

        self.grouping_started.emit(len(self.image_paths))
        image_groups = group_images(self.image_paths)
        self.grouping_finished.emit(len(image_groups))

        total_groups = len(image_groups)

        for i, group in enumerate(image_groups):
            if self._is_stopped:
                break

            self.ocr_started.emit(group.group_id)
            all_ocr_results = []
            record_status = "SUCCESS"
            error_msg = ""

            try:
                for image_path in group.image_paths:
                    if self._is_stopped:
                        break
                    ocr_result = ocr_image(image_path) # ocr_result is a DoclingDocument
                    if ocr_result:
                        all_ocr_results.append(ocr_result) # Change extend to append
            except Exception as e:
                record_status = "FAILED"
                error_msg = str(e)
                self.ocr_error.emit(group.group_id, error_msg)

            if self._is_stopped:
                break

            if all_ocr_results and record_status == "SUCCESS":
                # Create a single record for the group to merge info into.
                record = IDCardRecord(record_id=str(i + 1))
                try:
                    # Loop through results from all images (front and back)
                    # and update the same record.
                    for ocr_result in all_ocr_results:
                        record = extract_info(ocr_result, record=record)

                except Exception as e:
                    logging.error(f"Failed to extract info for group {group.group_id}: {e}", exc_info=True)
                    try:
                        # Log a concise summary of all OCR results in the group
                        for i, ocr_res in enumerate(all_ocr_results):
                            log_str = (
                                f"Problematic OCR data (Image {i+1}) - "
                                f"txts: {getattr(ocr_res, 'txts', 'N/A')}, "
                                f"scores: {getattr(ocr_res, 'scores', 'N/A')}"
                            )
                            logging.error(log_str)
                    except Exception as log_e:
                        logging.error(f"Could not log concise OCR data: {log_e}")

                    record_status = "FAILED"
                    error_msg = f"Info extraction failed: {e}"
                    record.status = "FAILED"
                    record.raw_ocr_output = error_msg

                # Final validation and appending the record
                if record_status == "SUCCESS" and (not record.name or not record.id_number):
                    record.status = "FAILED"
                    record_status = "FAILED"

                record.source_images = group.image_paths
                if not record.raw_ocr_output:
                    try:
                        # Store a summary if no specific error was recorded
                        record.raw_ocr_output = f"{len(all_ocr_results)} images processed."
                    except Exception:
                        record.raw_ocr_output = "Could not represent OCR data."
                app_state.records.append(record)
            else:
                # Create a failed record if OCR returns nothing or an error occurred
                record = IDCardRecord(
                    record_id=str(i + 1),
                    source_images=group.image_paths,
                    status="FAILED",
                    raw_ocr_output=error_msg
                )
                app_state.records.append(record)
                record_status = "FAILED"

            self.ocr_finished.emit(group.group_id, record_status)
            self.progress.emit(int(((i + 1) / total_groups) * 100))

        self.finished.emit(app_state)

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()

        self.setWindowTitle("身份证信息提取工具")

        # Load window geometry from settings
        self.settings = QSettings("Hutu", "IDCardOCR")
        geom = self.settings.value("geometry")
        if geom:
            self.restoreGeometry(geom)
        else:
            self.setGeometry(100, 100, 1200, 800)

        # App State
        self.app_state = AppState()
        self.selected_files = []
        self.worker = None

        # Tool Bar (Menu Bar removed)
        self.tool_bar = QToolBar("Main Tool Bar")
        self.tool_bar.setMovable(False)
        self.addToolBar(self.tool_bar)

        select_files_action = QAction("选择文件", self)
        select_files_action.triggered.connect(self.select_files)
        self.tool_bar.addAction(select_files_action)

        select_folder_action = QAction("选择文件夹", self)
        select_folder_action.triggered.connect(self.select_folder)
        self.tool_bar.addAction(select_folder_action)

        self.tool_bar.addSeparator()

        start_ocr_action = QAction("开始识别", self)
        start_ocr_action.triggered.connect(self.start_ocr)
        self.tool_bar.addAction(start_ocr_action)

        stop_ocr_action = QAction("停止识别", self)
        stop_ocr_action.triggered.connect(self.stop_ocr)
        self.tool_bar.addAction(stop_ocr_action)

        self.tool_bar.addSeparator()

        export_excel_action = QAction("导出Excel", self)
        export_excel_action.triggered.connect(self.export_excel)
        self.tool_bar.addAction(export_excel_action)

        # Status Bar
        self.status_bar = QStatusBar()
        self.setStatusBar(self.status_bar)
        self.status_bar.showMessage("准备就绪")

        # Create and add progress bar to the status bar
        self.progress_bar = QProgressBar(self)
        self.progress_bar.setTextVisible(True)
        self.progress_bar.setFormat("处理进度: %p%")
        self.progress_bar.setValue(0)
        self.progress_bar.setFixedWidth(300) # Give it a consistent size
        self.status_bar.addPermanentWidget(self.progress_bar)

        # Central Widget
        self.central_widget = QWidget()
        self.setCentralWidget(self.central_widget)

        # Main Layout (Horizontal for file list and table)
        self.main_layout = QHBoxLayout(self.central_widget)

        # Left Panel for File List
        self.left_panel = QVBoxLayout()
        self.file_list_widget = QListWidget()
        self.file_list_widget.setSelectionMode(QListWidget.ExtendedSelection)
        self.left_panel.addWidget(self.file_list_widget)

        self.remove_file_button = QPushButton("移除文件")
        self.remove_file_button.clicked.connect(self.remove_selected_files)
        self.left_panel.addWidget(self.remove_file_button)

        self.main_layout.addLayout(self.left_panel, 1) # 1/3 width for file list

        # Right Panel for Table View
        self.right_panel = QVBoxLayout()

        self.table_view = QTableView()
        self.table_view.setSelectionBehavior(QTableView.SelectRows)
        self.table_view.setContextMenuPolicy(Qt.CustomContextMenu)
        self.table_view.customContextMenuRequested.connect(self.show_table_context_menu)
        self.table_view.horizontalHeader().setSectionsMovable(True)
        self.table_view.horizontalHeader().setContextMenuPolicy(Qt.CustomContextMenu)
        self.table_view.horizontalHeader().customContextMenuRequested.connect(self.show_header_context_menu)
        self.table_model = RecordTableModel(self.app_state)
        self.table_view.setModel(self.table_model)

        # Hide status and raw_ocr_output columns by default
        try:
            status_index = self.app_state.column_settings['order'].index('status')
            self.table_view.setColumnHidden(status_index, True)
            raw_ocr_index = self.app_state.column_settings['order'].index('raw_ocr_output')
            self.table_view.setColumnHidden(raw_ocr_index, True)
        except ValueError:
            # Fails gracefully if columns are not in the model
            pass
        self.right_panel.addWidget(self.table_view)

        self.main_layout.addLayout(self.right_panel, 2) # 2/3 width for table

    def select_files(self):
        files, _ = QFileDialog.getOpenFileNames(
            self, "选择一个或多个文件", "", "Images (*.png *.xpm *.jpg *.bmp *.gif)"
        )
        if files:
            self.add_files_to_list(files)

    def select_folder(self):
        folder = QFileDialog.getExistingDirectory(self, "选择文件夹")
        if folder:
            image_paths = []
            for filename in os.listdir(folder):
                if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.bmp', '.gif')):
                    image_paths.append(os.path.join(folder, filename))
            if image_paths:
                self.add_files_to_list(image_paths)

    def add_files_to_list(self, files):
        for f in files:
            if f not in self.selected_files:
                self.selected_files.append(f)
                self.file_list_widget.addItem(f)
        self.status_bar.showMessage(
            f"已添加 {len(files)} 个文件。"
            f"当前共 {len(self.selected_files)} 个文件待处理。"
        )

    def remove_selected_files(self):
        for item in self.file_list_widget.selectedItems():
            self.selected_files.remove(item.text())
            self.file_list_widget.takeItem(self.file_list_widget.row(item))
        self.status_bar.showMessage(
            f"已移除文件。当前共 {len(self.selected_files)} 个文件待处理。"
        )

    def start_ocr(self):
        self.run_ocr_worker()

    def stop_ocr(self):
        if self.worker and self.worker.isRunning():
            self.worker.stop()
            self.status_bar.showMessage("识别已停止。")

    def run_ocr_worker(self):
        if not self.selected_files:
            self.status_bar.showMessage("没有文件可供识别！")
            return
        self.worker = Worker(self.selected_files)
        self.worker.finished.connect(self.on_ocr_finished)
        self.worker.progress.connect(self.on_ocr_progress)
        self.worker.grouping_started.connect(self.on_grouping_started)
        self.worker.grouping_finished.connect(self.on_grouping_finished)
        self.worker.ocr_started.connect(self.on_ocr_started)
        self.worker.ocr_finished.connect(self.on_ocr_finished_single)
        self.worker.ocr_error.connect(self.on_ocr_error)
        self.worker.start()
        self.status_bar.showMessage("正在识别中...")

    def on_ocr_finished(self, new_app_state):
        self.app_state = new_app_state
        self.table_model.update_data(new_app_state)
        self.status_bar.showMessage(
            f"识别完成！共找到 {len(self.app_state.records)} 条记录。"
        )
        self.progress_bar.setValue(0)

    def on_ocr_progress(self, percent):
        self.progress_bar.setValue(percent)
        self.status_bar.showMessage(f"正在识别中... {percent}%")

    def on_grouping_started(self, total_files):
        self.status_bar.showMessage(f"开始分组 {total_files} 个文件...")

    def on_grouping_finished(self, total_groups):
        self.status_bar.showMessage(f"分组完成！共找到 {total_groups} 组。")

    def on_ocr_started(self, group_id):
        self.status_bar.showMessage(f"正在识别组: {group_id}...")

    def on_ocr_finished_single(self, group_id, status):
        self.status_bar.showMessage(f"组 {group_id} 识别{status}。")

    def on_ocr_error(self, group_id, error_message):
        self.status_bar.showMessage(f"组 {group_id} 识别错误: {error_message}")

    def export_excel(self):
        if not self.app_state.records:
            self.status_bar.showMessage("没有数据可导出！")
            return

        file_name, _ = QFileDialog.getSaveFileName(
            self, "保存Excel文件", "", "Excel Files (*.xlsx)"
        )
        if file_name:
            export_to_excel(self.app_state, file_name, self.table_view)
            self.status_bar.showMessage(f"数据已导出到 {file_name}")

    def show_header_context_menu(self, pos):
        header = self.table_view.horizontalHeader()
        menu = QMenu(self)

        # --- Column Visibility Actions ---
        menu.addSection("显示/隐藏列")
        for i in range(self.table_model.columnCount()):
            column_key = self.app_state.column_settings['order'][i]
            column_name = self.app_state.column_settings['custom_names'].get(column_key, column_key)
            
            action = QAction(column_name, self, checkable=True)
            action.setChecked(not self.table_view.isColumnHidden(i))
            # Use a lambda to pass the column index to the slot
            action.toggled.connect(lambda checked, index=i: self.toggle_column(index, checked))
            menu.addAction(action)

        menu.addSeparator()

        # --- Other Actions (like rename) ---
        clicked_logical_index = header.logicalIndexAt(pos)
        if clicked_logical_index >= 0:
            rename_action = menu.addAction("重命名当前列")
            action = menu.exec(header.mapToGlobal(pos))
            if action == rename_action:
                self.rename_column(clicked_logical_index)
        else:
            # If not clicking on a specific header, just show the menu
            menu.exec(header.mapToGlobal(pos))

    def toggle_column(self, index, checked):
        """Slot to hide/show the column based on the action's state."""
        self.table_view.setColumnHidden(index, not checked)

    def rename_column(self, logical_index):
        current_name = self.table_model.headerData(
            logical_index, Qt.Horizontal, Qt.DisplayRole
        )
        new_name, ok = QInputDialog.getText(
            self, "重命名列", "输入新的列名称:", text=current_name
        )
        if ok and new_name:
            column_key = self.app_state.column_settings['order'][logical_index]
            self.app_state.column_settings['custom_names'][column_key] = new_name
            self.table_model.headerDataChanged.emit(
                Qt.Horizontal, logical_index, logical_index
            )

    def closeEvent(self, event):
        """Save window geometry on close."""
        self.settings.setValue("geometry", self.saveGeometry())
        super().closeEvent(event)

    def show_table_context_menu(self, pos):
        """Show context menu for the table view."""
        menu = QMenu(self)
        
        copy_action = QAction("复制", self)
        copy_action.triggered.connect(self.copy_selection)
        
        paste_action = QAction("粘贴", self)
        paste_action.setEnabled(False)  # Paste is complex, disable for now

        # Enable copy only if there is a selection
        if not self.table_view.selectionModel().hasSelection():
            copy_action.setEnabled(False)

        menu.addAction(copy_action)
        menu.addAction(paste_action)
        
        menu.exec(self.table_view.viewport().mapToGlobal(pos))

    def copy_selection(self):
        """Copy the selected rows to the clipboard as tab-separated text."""
        selection_model = self.table_view.selectionModel()
        if not selection_model.hasSelection():
            return

        # Get the data for the selected rows
        rows = sorted(list(set(index.row() for index in selection_model.selectedIndexes())))
        
        clipboard_string = ""
        for row in rows:
            row_data = []
            for column in range(self.table_model.columnCount()):
                if not self.table_view.isColumnHidden(column):
                    cell_value = self.table_model.index(row, column).data()
                    row_data.append(str(cell_value) if cell_value is not None else "")
            clipboard_string += "\t".join(row_data) + "\n"

        clipboard = QApplication.clipboard()
        clipboard.setText(clipboard_string)
        self.status_bar.showMessage(f"已复制 {len(rows)} 行数据。")
