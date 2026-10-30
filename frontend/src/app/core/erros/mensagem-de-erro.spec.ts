import { HttpErrorResponse } from '@angular/common/http';
import { ehVersaoDesatualizada, mensagemDeErro } from './mensagem-de-erro';

function erro409(codigo: string) {
  return new HttpErrorResponse({ status: 409, error: { status: 409, codigo } });
}

describe('mensagemDeErro', () => {
  it('distingue o 409 de horário ocupado do 409 de versão desatualizada', () => {
    expect(mensagemDeErro(erro409('CONFLITO_DE_HORARIO'))).toContain('horário já está reservado');
    expect(mensagemDeErro(erro409('VERSAO_DESATUALIZADA'))).toContain('alterada por outra pessoa');
  });

  it('usa o texto da API para regra de negócio (422)', () => {
    const erro = new HttpErrorResponse({
      status: 422,
      error: { codigo: 'RN02', detail: 'Não é permitido reservar com início no passado.' },
    });
    expect(mensagemDeErro(erro)).toBe('Não é permitido reservar com início no passado.');
  });
});

describe('ehVersaoDesatualizada', () => {
  it('só é verdadeiro para o 409 de versão', () => {
    expect(ehVersaoDesatualizada(erro409('VERSAO_DESATUALIZADA'))).toBe(true);
    expect(ehVersaoDesatualizada(erro409('CONFLITO_DE_HORARIO'))).toBe(false);
  });
});
