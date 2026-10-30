import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Cabecalho } from './shared/cabecalho';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Cabecalho],
  template: `
    <app-cabecalho />
    <main class="conteudo">
      <router-outlet />
    </main>
  `,
})
export class App {}
