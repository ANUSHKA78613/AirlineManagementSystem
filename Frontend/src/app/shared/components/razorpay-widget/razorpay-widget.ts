import { Component, Input, Output, EventEmitter, OnInit, NgZone } from '@angular/core';

export interface RazorpayOptions {
  key: string;
  amount: number;
  currency: string;
  name: string;
  description: string;
  order_id?: string;
  prefill?: {
    method?: string;
    name?: string;
    email?: string;
    contact?: string;
  };
}

declare var Razorpay: any;

@Component({
  selector: 'app-razorpay-widget',
  standalone: true,
  templateUrl: './razorpay-widget.html',
  styleUrl: './razorpay-widget.css'
})
export class RazorpayWidget implements OnInit {
  @Input() options!: RazorpayOptions;
  @Input() buttonText: string = 'Pay Now';
  @Output() paymentSuccess = new EventEmitter<any>();
  @Output() paymentError = new EventEmitter<any>();
  @Output() paymentClosed = new EventEmitter<void>();

  private successFired = false;

  constructor(private zone: NgZone) {}

  ngOnInit() {
    this.loadScript('https://checkout.razorpay.com/v1/checkout.js');
  }

  loadScript(src: string) {
    if (document.querySelector(`script[src="${src}"]`)) return;
    const script = document.createElement('script');
    script.src = src;
    script.async = true;
    document.body.appendChild(script);
  }

  open() {
    if (typeof Razorpay === 'undefined') {
      console.error('Razorpay SDK not loaded');
      return;
    }

    this.successFired = false;

    const config: any = {
      ...this.options,
      method: {
        upi: true,
        card: true,
        netbanking: true,
        wallet: true,
        paylater: true,
        qr: true
      },
      handler: (response: any) => {
        this.successFired = true;
        this.zone.run(() => this.paymentSuccess.emit(response));
      },
      modal: {
        ondismiss: () => {
          // ONLY fire paymentClosed if success didn't already fire
          if (!this.successFired) {
            this.zone.run(() => this.paymentClosed.emit());
          }
        }
      }
    };

    const rzp = new Razorpay(config);
    rzp.on('payment.failed', (response: any) => {
      this.zone.run(() => this.paymentError.emit(response.error));
    });

    rzp.open();
  }
}
