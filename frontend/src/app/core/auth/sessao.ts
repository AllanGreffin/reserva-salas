import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { map, Observable, tap } from 'rxjs';

export type Perfil = 'Comum' | 'Administrador';

export interface UsuarioLogado {
  id: number;
  nome: string;
  perfil: Perfil;
}

interface DadosSessao {
  token: string;
  expiraEm: string;
  usuario: UsuarioLogado;
}

const CHAVE = 'reserva-salas.sessao';

/**
 * Guarda o token em sessionStorage: sobrevive ao F5, é apagado ao fechar a aba e não é compartilhado
 * entre abas. Como qualquer storage acessível por JavaScript, pode ser lido por um script injetado
 * (XSS); a mitigação é impedir o XSS e a expiração curta do token (ver README).
 */
@Injectable({ providedIn: 'root' })
export class Sessao {
  private readonly http = inject(HttpClient);
  private readonly dados = signal<DadosSessao | null>(lerDoStorage());

  readonly usuario = computed(() => this.dados()?.usuario ?? null);

  login(login: string, senha: string): Observable<void> {
    return this.http.post<DadosSessao>('/api/auth/login', { login, senha }).pipe(
      tap((dados) => {
        sessionStorage.setItem(CHAVE, JSON.stringify(dados));
        this.dados.set(dados);
      }),
      map(() => undefined),
    );
  }

  /** Token válido e não expirado, ou null. */
  token(): string | null {
    const dados = this.dados();
    return dados && new Date(dados.expiraEm).getTime() > Date.now() ? dados.token : null;
  }

  estaAutenticado(): boolean {
    return this.token() !== null;
  }

  encerrar(): void {
    sessionStorage.removeItem(CHAVE);
    this.dados.set(null);
  }
}

function lerDoStorage(): DadosSessao | null {
  try {
    const texto = sessionStorage.getItem(CHAVE);
    return texto ? (JSON.parse(texto) as DadosSessao) : null;
  } catch {
    return null;
  }
}
