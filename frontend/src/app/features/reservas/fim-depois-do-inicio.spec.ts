import { FormControl, FormGroup } from '@angular/forms';
import { fimDepoisDoInicio } from './fim-depois-do-inicio';

function grupo(inicio: string, fim: string) {
  return new FormGroup(
    { inicio: new FormControl(inicio), fim: new FormControl(fim) },
    { validators: fimDepoisDoInicio },
  );
}

describe('fimDepoisDoInicio', () => {
  it('aceita fim posterior ao início', () => {
    expect(grupo('2030-01-15T10:00', '2030-01-15T11:00').valid).toBe(true);
  });

  it('rejeita fim igual ao início', () => {
    expect(grupo('2030-01-15T10:00', '2030-01-15T10:00').hasError('fimAntesDoInicio')).toBe(true);
  });

  it('rejeita fim antes do início', () => {
    expect(grupo('2030-01-15T10:00', '2030-01-15T09:00').hasError('fimAntesDoInicio')).toBe(true);
  });

  it('não acusa erro enquanto um dos campos estiver vazio', () => {
    expect(grupo('', '2030-01-15T09:00').hasError('fimAntesDoInicio')).toBe(false);
    expect(grupo('2030-01-15T10:00', '').hasError('fimAntesDoInicio')).toBe(false);
  });
});
