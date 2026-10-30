import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { DadosReserva, Reserva, ReservasApi, Sala } from '../../core/api/reservas-api';
import { localParaUtc, utcParaLocal } from '../../core/datas';
import { mensagemDeErro } from '../../core/erros/mensagem-de-erro';
import { Alerta } from '../../shared/alerta';
import { fimDepoisDoInicio } from './fim-depois-do-inicio';

const TAMANHO_MAXIMO_TITULO = 200;

/** Criação e edição no mesmo componente: o modo vem da rota (com ou sem :id). */
@Component({
  selector: 'app-reserva-form',
  imports: [ReactiveFormsModule, RouterLink, Alerta],
  template: `
    <section class="cartao estreito">
      <h1>{{ edicao() ? 'Editar reserva' : 'Nova reserva' }}</h1>

      @if (erro(); as mensagem) {
        <app-alerta [mensagem]="mensagem" />
      }
      @if (bloqueada()) {
        <app-alerta
          tipo="aviso"
          mensagem="Você não pode editar esta reserva: ela é de outro usuário, foi cancelada ou já terminou."
        />
      }

      <form [formGroup]="form" (ngSubmit)="salvar()">
        <label>
          Sala
          <select formControlName="salaId">
            <option [ngValue]="null" disabled>Selecione…</option>
            @for (sala of salas(); track sala.id) {
              <option [ngValue]="sala.id">{{ sala.nome }} ({{ sala.capacidade }} lugares)</option>
            }
          </select>
        </label>
        @if (form.controls.salaId.touched && form.controls.salaId.hasError('required')) {
          <span class="erro-campo">Selecione a sala.</span>
        }

        <label>
          Título
          <input formControlName="titulo" [maxlength]="tamanhoMaximoTitulo" />
        </label>
        @if (form.controls.titulo.touched && form.controls.titulo.hasError('required')) {
          <span class="erro-campo">Informe o título.</span>
        }

        <div class="linha">
          <label>
            Início
            <input formControlName="inicio" type="datetime-local" />
          </label>
          <label>
            Fim
            <input formControlName="fim" type="datetime-local" />
          </label>
        </div>
        @if (form.controls.inicio.touched && form.controls.inicio.hasError('required')) {
          <span class="erro-campo">Informe o início.</span>
        }
        @if (form.controls.fim.touched && form.controls.fim.hasError('required')) {
          <span class="erro-campo">Informe o fim.</span>
        }
        @if (form.hasError('fimAntesDoInicio') && (form.controls.inicio.touched || form.controls.fim.touched)) {
          <span class="erro-campo">O fim deve ser posterior ao início.</span>
        }
        <small class="dica">Horários no fuso do seu navegador.</small>

        <div class="acoes-form">
          <a routerLink="/reservas" class="botao secundario">Voltar</a>
          <button type="submit" [disabled]="salvando() || bloqueada()">
            {{ salvando() ? 'Salvando…' : 'Salvar' }}
          </button>
        </div>
      </form>
    </section>
  `,
})
export class ReservaForm implements OnInit {
  /** Parâmetro :id da rota (withComponentInputBinding); ausente na criação. */
  readonly id = input<string>();

  private readonly api = inject(ReservasApi);
  private readonly router = inject(Router);

  protected readonly tamanhoMaximoTitulo = TAMANHO_MAXIMO_TITULO;
  protected readonly edicao = computed(() => this.id() !== undefined);
  protected readonly salas = signal<Sala[]>([]);
  protected readonly salvando = signal(false);
  protected readonly bloqueada = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      salaId: [null as number | null, Validators.required],
      titulo: ['', [Validators.required, Validators.maxLength(TAMANHO_MAXIMO_TITULO)]],
      inicio: ['', Validators.required],
      fim: ['', Validators.required],
    },
    { validators: fimDepoisDoInicio },
  );

  ngOnInit(): void {
    this.api.listarSalas().subscribe({
      next: (salas) => this.salas.set(salas),
      error: (erro: unknown) => this.erro.set(mensagemDeErro(erro)),
    });

    if (this.edicao()) this.carregarReserva(Number(this.id()));
  }

  protected salvar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const valor = this.form.getRawValue();
    const dados: DadosReserva = {
      salaId: valor.salaId!,
      titulo: valor.titulo.trim(),
      inicio: localParaUtc(valor.inicio),
      fim: localParaUtc(valor.fim),
    };

    this.salvando.set(true);
    this.erro.set(null);

    const operacao: Observable<Reserva> = this.edicao()
      ? this.api.editar(Number(this.id()), dados)
      : this.api.criar(dados);

    operacao.subscribe({
      next: () => this.router.navigate(['/reservas']),
      error: (erro: unknown) => {
        this.salvando.set(false);
        this.erro.set(mensagemDeErro(erro));
        // 403/404: não há o que corrigir no formulário; os demais (409, 422, 400) permitem ajustar e reenviar.
        if (erro instanceof HttpErrorResponse && (erro.status === 403 || erro.status === 404)) {
          this.bloqueada.set(true);
        }
      },
    });
  }

  private carregarReserva(id: number): void {
    this.api.obter(id).subscribe({
      next: (reserva) => {
        this.form.setValue({
          salaId: reserva.sala.id,
          titulo: reserva.titulo,
          inicio: utcParaLocal(reserva.inicio),
          fim: utcParaLocal(reserva.fim),
        });
        this.bloqueada.set(!reserva.podeEditar);
      },
      error: (erro: unknown) => {
        this.erro.set(mensagemDeErro(erro));
        this.bloqueada.set(true);
      },
    });
  }
}
