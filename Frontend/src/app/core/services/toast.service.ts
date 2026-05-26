import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { ToastMessage } from '../models';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private toasts = new BehaviorSubject<ToastMessage[]>([]);
  toasts$ = this.toasts.asObservable();

  success(message: string, duration = 4000) { this.show('success', message, duration); }
  error(message: string, duration = 5000) { this.show('error', message, duration); }
  info(message: string, duration = 4000) { this.show('info', message, duration); }
  warning(message: string, duration = 4000) { this.show('warning', message, duration); }

  private show(type: ToastMessage['type'], message: string, duration: number) {
    const id = Math.random().toString(36).substring(2, 9);
    const toast: ToastMessage = { id, type, message, duration };
    this.toasts.next([...this.toasts.value, toast]);
    setTimeout(() => this.dismiss(id), duration);
  }

  dismiss(id: string) {
    this.toasts.next(this.toasts.value.filter(t => t.id !== id));
  }
}
