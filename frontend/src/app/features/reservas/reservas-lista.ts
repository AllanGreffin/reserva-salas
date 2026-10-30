import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Reserva, ReservasApi } from '../../core/api/reservas-api';
import { mensagemDeErro } from '../../core/erros/mensagem-de-erro';
import { Alerta } from '../../shared/alerta';

@Component({
  selector: 'app-reservas-lista',
  imports: [DatePipe, RouterLink, Alerta],
  host: { '(document:visibilitychange)': 'aoMudarVisibilidade()' },
  templateUrl: './reservas-lista.html',
})
export class ReservasLista implements OnInit {
  private readonly api = inject(ReservasApi);

  protected readonly reservas = signal<Reserva[]>([]);
  protected readonly carregando = signal(false);
  protected readonly cancelando = signal<number | null>(null);
  protected readonly erro = signal<string | null>(null);

  ngOnInit(): void {
    this.carregar();
  }

  protected cancelar(reserva: Reserva): void {
    if (!confirm(`Cancelar a reserva "${reserva.titulo}"?`)) return;

    this.cancelando.set(reserva.id);
    this.erro.set(null);
    this.api.cancelar(reserva.id).subscribe({
      next: () => {
        this.cancelando.set(null);
        this.carregar();
      },
      // Os botões refletem o momento da consulta; se o estado mudou, o backend recusa e a lista
      // é recarregada para mostrar a situação atual.
      error: (erro: unknown) => {
        this.cancelando.set(null);
        this.erro.set(mensagemDeErro(erro));
        this.carregar();
      },
    });
  }

  /** Ao voltar para a aba, recarrega: a lista pode ter mudado enquanto ela estava em segundo plano. */
  protected aoMudarVisibilidade(): void {
    if (document.visibilityState === 'visible') this.carregar();
  }

  private carregar(): void {
    this.carregando.set(true);
    this.api.listar().subscribe({
      next: (reservas) => {
        this.reservas.set(reservas);
        this.carregando.set(false);
      },
      error: (erro: unknown) => {
        this.erro.set(mensagemDeErro(erro));
        this.carregando.set(false);
      },
    });
  }
}
