import { formatNumber } from '@angular/common';
import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'signedNumber' })
export class SignedNumberPipe implements PipeTransform {
  transform(value: number | null | undefined, digitsInfo = '1.0-0'): string {
    if (value === null || value === undefined) {
      return '–';
    }
    const formatted = formatNumber(value, 'de', digitsInfo);
    return value > 0 ? `+${formatted}` : formatted;
  }
}
