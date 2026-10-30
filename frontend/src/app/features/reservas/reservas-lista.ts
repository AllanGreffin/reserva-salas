import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, merge, of, Subject, switchMap } from 'rxjs';
import { FiltroReservas, Pagina, Reserva, ReservasApi, Sala } from '../../core/api/reservas-api';
import { fimDoDiaUtc, inicioDoDiaUtc } from '../../core/datas';
import { mensagemDeErro } from '../../core/erros/mensagem-de-erro';
import { Alerta } from '../../shared/alerta';

/** O dia final do filtro não pode ser anterior ao inicial. */
function periodoValido(grupo: AbstractControl): ValidationErrors | null {
  const { de, ate } = grupo.value as { de: string; ate: string };
  return de && ate && ate < de ? { periodoInvertido: true } : null;
}

/**
 * Filtros e página ficam na URL (?sala=&de=&ate=&pagina=): F5, voltar e links compartilhados mantêm o
 * estado. Mudar um filtro é navegar; a consulta reage à URL.
 */
@Component({
  selector: 'app-reservas-lista',
  imports: [DatePipe, RouterLink, ReactiveFormsModule, Alerta],
  host: { '(document:visibilitychange)': 'aoMudarVisibilidade()' },
  templateUrl: './reservas-lista.html',
})
export class ReservasLista {
  // Query params, via withComponentInputBinding. "de" e "ate" são dias (yyyy-MM-dd) no fuso do navegador.
  readonly sala = input<string>();
  readonly de = input<string>();
  readonly ate = input<string>();
  readonly pagina = input<string>();

  private readonly api = inject(ReservasApi);
  private readonly router = inject(Router);

  protected readonly salas = signal<Sala[]>([]);
  protected readonly resultado = signal<Pagina<Reserva> | null>(null);
  protected readonly carregando = signal(false);
  protected readonly cancelando = signal<number | null>(null);
  protected readonly erro = signal<string | null>(null);

  protected readonly filtros = inject(NonNullableFormBuilder).group(
    { sala: [''], de: [''], ate: [''] },
    { validators: periodoValido },
  );

  protected readonly paginaAtual = computed(() => Math.max(1, Number(this.pagina()) || 1));
  protected readonly filtrando = computed(() => Boolean(this.sala() || this.de() || this.ate()));

  private readonly consulta = computed<FiltroReservas>(() => {
    const sala = this.sala();
    const de = this.de();
    const ate = this.ate();
    return {
      salaId: sala ? Number(sala) : undefined,
      de: de ? inicioDoDiaUtc(de) : undefined,
      ate: ate ? fimDoDiaUtc(ate) : undefined,
      pagina: this.paginaAtual(),
    };
  });

  private readonly recarregar = new Subject<void>();

  constructor() {
    // O formulário de filtros acompanha a URL (inclusive no F5 e no voltar).
    effect(() => {
      this.filtros.setValue({
        sala: this.sala() ?? '',
        de: this.de() ?? '',
        ate: this.ate() ?? '',
      });
    });

    // switchMap descarta respostas de consultas antigas se o usuário trocar de página ou filtro rápido.
    merge(toObservable(this.consulta), this.recarregar.pipe(map(() => this.consulta())))
      .pipe(
        switchMap((consulta) => {
          this.carregando.set(true);
          return this.api.listar(consulta).pipe(
            catchError((erro: unknown) => {
              this.erro.set(mensagemDeErro(erro));
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((resultado) => {
        if (resultado) this.resultado.set(resultado);
        this.carregando.set(false);
      });

    this.api.listarSalas().subscribe({
      next: (salas) => this.salas.set(salas),
      error: (erro: unknown) => this.erro.set(mensagemDeErro(erro)),
    });
  }

  protected filtrar(): void {
    if (this.filtros.invalid) return;
    const { sala, de, ate } = this.filtros.getRawValue();
    this.erro.set(null);
    this.router.navigate([], {
      queryParams: { sala: sala || null, de: de || null, ate: ate || null },
    });
  }

  protected limparFiltros(): void {
    this.erro.set(null);
    this.router.navigate([], { queryParams: {} });
  }

  protected irParaPagina(pagina: number): void {
    this.erro.set(null);
    this.router.navigate([], {
      queryParams: { pagina: pagina === 1 ? null : pagina },
      queryParamsHandling: 'merge',
    });
  }

  protected cancelar(reserva: Reserva): void {
    if (!confirm(`Cancelar a reserva "${reserva.titulo}"?`)) return;

    this.cancelando.set(reserva.id);
    this.erro.set(null);
    this.api.cancelar(reserva.id).subscribe({
      next: () => {
        this.cancelando.set(null);
        this.recarregar.next();
      },
      // Os botões refletem o momento da consulta; se o estado mudou, o backend recusa e a lista
      // é recarregada (na mesma página) para mostrar a situação atual.
      error: (erro: unknown) => {
        this.cancelando.set(null);
        this.erro.set(mensagemDeErro(erro));
        this.recarregar.next();
      },
    });
  }

  /** Ao voltar para a aba, recarrega: a lista pode ter mudado enquanto ela estava em segundo plano. */
  protected aoMudarVisibilidade(): void {
    if (document.visibilityState === 'visible') this.recarregar.next();
  }
}
