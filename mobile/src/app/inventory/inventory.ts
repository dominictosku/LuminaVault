import { Component, inject } from '@angular/core';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';
import { MessageService, ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';

interface Item {
  name: string;
  quantity: number;
}

@Component({
  selector: 'app-inventory',
  imports: [TableModule, ButtonModule, InputTextModule, FormsModule, ConfirmDialogModule, ToastModule, DialogModule],
  providers: [MessageService, ConfirmationService],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css',
})
export class Inventory {

  confirmationService = inject(ConfirmationService);
  messageService = inject(MessageService);
  items: Item[] = [];
  visible: boolean = false;

  showDialog() {
    this.visible = true;
  }

  newItem: Item = {
    name: '',
    quantity: 0,
  };

  addItem() {
    if (!this.newItem.name) return;

    this.items = [...this.items, { ...this.newItem }];
    this.newItem = { name: '', quantity: 0 };
    this.visible = false;
  }

  removeItem(index: number) {
    this.items = this.items.filter((_, i) => i !== index);
  }


  confirm(event: Event, index: number) {
    this.confirmationService.confirm({
      target: event.target as EventTarget,
      message: 'Do you want to delete this record?',
      header: 'Danger Zone',
      icon: 'pi pi-info-circle',
      rejectLabel: 'Cancel',
      rejectButtonProps: {
        label: 'Cancel',
        severity: 'secondary',
        outlined: true
      },
      acceptButtonProps: {
        label: 'Delete',
        severity: 'danger'
      },

      accept: () => {
        this.removeItem(index);
        this.messageService.add({ severity: 'info', summary: 'Confirmed', detail: 'Record deleted' });
      },
      reject: () => {
        this.messageService.add({ severity: 'error', summary: 'Rejected', detail: 'You have rejected' });
      }
    });
  }
}
