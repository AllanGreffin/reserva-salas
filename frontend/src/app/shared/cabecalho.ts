import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Sessao } from '../core/auth/sessao';

@Component({
  selector: 'app-cabecalho',
  imports: [RouterLink],
  templateUrl: './cabecalho.html',
})
export class Cabecalho {
  protected readonly sessao = inject(Sessao);
  private readonly router = inject(Router);

  protected sair(): void {
    this.sessao.encerrar();
    this.router.navigate(['/login']);
  }
}
