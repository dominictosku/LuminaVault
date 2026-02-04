import { Component } from '@angular/core';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';

interface Item {
  name: string;
  quantity: number;
}

@Component({
  selector: 'app-inventory',
  imports: [TableModule, ButtonModule, InputTextModule, FormsModule],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css',
})
export class Inventory {
  items: Item[] = [];

  newItem: Item = {
    name: '',
    quantity: 0,
  };

  addItem() {
    if (!this.newItem.name) return;

    this.items = [...this.items, { ...this.newItem }];
    this.newItem = { name: '', quantity: 0 };
  }

  removeItem(index: number) {
    this.items = this.items.filter((_, i) => i !== index);
  }
}
