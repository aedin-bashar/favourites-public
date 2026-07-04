import { type AbstractControl, type ValidationErrors } from '@angular/forms';

export function nonBlankValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (typeof value !== 'string') {
    return null;
  }

  return value.trim().length > 0 ? null : { blank: true };
}
