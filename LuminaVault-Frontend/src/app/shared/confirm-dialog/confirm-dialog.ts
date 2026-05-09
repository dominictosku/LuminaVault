import { Component, HostListener, inject } from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.scss',
})
export class ConfirmDialogComponent {
  protected dialog = inject(ConfirmDialogService);

  @HostListener('document:keydown.escape')
  onEscape() {
    if (this.dialog.request()) this.dialog.close(false);
  }
}
