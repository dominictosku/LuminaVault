import { Component, inject } from '@angular/core';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';
import { MessageService, ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputMaskModule } from 'primeng/inputmask';

interface Item {
  name: string;
  quantity: number;
  date_of_order: Date;
  price: number;
  category: Category
}

enum Category {
  IT,
  Food,
  Clothing
}

@Component({
  selector: 'app-inventory',
  imports: [TableModule, ButtonModule, DatePickerModule, InputMaskModule, InputTextModule, FormsModule, ConfirmDialogModule, ToastModule, DialogModule, SelectModule],
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
    date_of_order: new Date(),
    category: Category.IT,
    price: 0
  };


  categoryOptions = Object.values(Category).map(cat => ({
    name: cat,
    value: cat
  }));

  addItem() {
    if (!this.newItem.name) return;

    this.items = [...this.items, { ...this.newItem }];
    this.newItem = { name: '', quantity: 0, date_of_order: new Date(), category: Category.IT, price: 0 };
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
