import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Sessao } from '../../core/auth/sessao';
import { mensagemDeErro } from '../../core/erros/mensagem-de-erro';
import { Alerta } from '../../shared/alerta';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, Alerta],
  templateUrl: './login.html',
})
export class Login {
  // Preenchidos a partir da query string (withComponentInputBinding).
  readonly retorno = input<string>();
  readonly motivo = input<string>();

  private readonly sessao = inject(Sessao);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    login: ['', Validators.required],
    senha: ['', Validators.required],
  });
  protected readonly enviando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected entrar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.enviando.set(true);
    this.erro.set(null);
    const { login, senha } = this.form.getRawValue();

    this.sessao.login(login, senha).subscribe({
      next: () => this.router.navigateByUrl(this.destinoSeguro()),
      error: (erro: unknown) => {
        this.erro.set(mensagemDeErro(erro));
        this.enviando.set(false);
      },
    });
  }

  /** Só aceita caminhos internos: um "retorno" externo na URL não pode redirecionar para fora do app. */
  private destinoSeguro(): string {
    const retorno = this.retorno();
    return retorno?.startsWith('/') && !retorno.startsWith('//') ? retorno : '/reservas';
  }
}
