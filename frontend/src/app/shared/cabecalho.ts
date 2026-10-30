import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Sessao } from '../core/auth/sessao';

@Component({
  selector: 'app-cabecalho',
  imports: [RouterLink],
  template: `
    <header class="cabecalho">
      <a routerLink="/reservas" class="marca">Reserva de Salas</a>
      @if (sessao.usuario(); as usuario) {
        <div class="usuario">
          <span>{{ usuario.nome }} <small>({{ usuario.perfil }})</small></span>
          <button type="button" class="secundario" (click)="sair()">Sair</button>
        </div>
      }
    </header>
  `,
})
export class Cabecalho {
  protected readonly sessao = inject(Sessao);
  private readonly router = inject(Router);

  protected sair(): void {
    this.sessao.encerrar();
    this.router.navigate(['/login']);
  }
}
