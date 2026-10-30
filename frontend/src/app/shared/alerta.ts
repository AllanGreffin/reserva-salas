import { Component, input } from '@angular/core';

/** Mensagem de feedback. O texto é interpolado, então nunca é interpretado como HTML. */
@Component({
  selector: 'app-alerta',
  template: `<p class="alerta" [class]="tipo()" role="alert">{{ mensagem() }}</p>`,
})
export class Alerta {
  readonly mensagem = input.required<string>();
  readonly tipo = input<'erro' | 'aviso' | 'sucesso'>('erro');
}
