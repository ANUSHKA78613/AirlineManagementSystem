import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'rupeeCurrency',
  standalone: true
})
export class RupeeCurrencyPipe implements PipeTransform {
  transform(value: string | number | null | undefined, digitsInfo: string = '1.0-0'): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    // Try converting the value to a number. It can be a string like "$12,450" inside the mock HTML template, so strip non-numeric except dot
    let numericValue = typeof value === 'string' 
      ? parseFloat(value.replace(/[^0-9.]/g, '')) 
      : value;

    if (isNaN(numericValue)) return String(value);

    // Format for Indian locale (INR formatting for thousands separator like 1,00,000)
    const formatter = new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    });

    return formatter.format(numericValue);
  }
}
