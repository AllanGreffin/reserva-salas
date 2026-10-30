import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Validador do grupo (RN01 no cliente): o fim deve ser posterior ao início. Fica no FormGroup porque
 * envolve dois campos. Campos vazios são responsabilidade do Validators.required de cada um.
 */
export const fimDepoisDoInicio: ValidatorFn = (grupo: AbstractControl): ValidationErrors | null => {
  const inicio = grupo.get('inicio')?.value as string | undefined;
  const fim = grupo.get('fim')?.value as string | undefined;

  if (!inicio || !fim) return null;
  return new Date(fim) > new Date(inicio) ? null : { fimAntesDoInicio: true };
};
