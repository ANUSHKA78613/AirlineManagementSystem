import { Component, Input, Output, EventEmitter, TemplateRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgTemplateOutlet } from '@angular/common';

export interface TableColumn {
  field: string;
  header: string;
}

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './data-table.html'
})
export class DataTable {
  @Input() columns: TableColumn[] = [];
  @Input() data: any[] = [];
  @Input() loading: boolean = false;
  @Input() showEdit: boolean = false;
  @Input() showDelete: boolean = false;
  @Input() showToggle: boolean = false;
  @Input() actionsTemplate: TemplateRef<any> | null = null;

  @Output() edit = new EventEmitter<any>();
  @Output() delete = new EventEmitter<any>();
  @Output() toggleState = new EventEmitter<any>();
  @Output() actionClicked = new EventEmitter<{action: string, row: any}>();

  get hasActions(): boolean {
    return this.showEdit || this.showDelete || this.showToggle || (this.actionsTemplate !== null && this.actionsTemplate !== undefined);
  }
}
