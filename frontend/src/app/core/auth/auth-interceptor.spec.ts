import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';
import { Sessao } from './sessao';

const CHAVE = 'reserva-salas.sessao';

function salvarSessao(expiraEm: Date) {
  sessionStorage.setItem(
    CHAVE,
    JSON.stringify({
      token: 'token-de-teste',
      expiraEm: expiraEm.toISOString(),
      usuario: { id: 1, nome: 'Ana', perfil: 'Comum' },
    }),
  );
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let servidor: HttpTestingController;
  let router: Router;

  function configurar() {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    servidor = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => servidor.verify());

  it('anexa o token às requisições para a API', () => {
    salvarSessao(new Date(Date.now() + 60_000));
    configurar();

    http.get('/api/reservas').subscribe();

    const req = servidor.expectOne('/api/reservas');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-de-teste');
    req.flush([]);
  });

  it('não anexa o token a outros domínios', () => {
    salvarSessao(new Date(Date.now() + 60_000));
    configurar();

    http.get('https://outro-dominio.example/api/dados').subscribe();

    const req = servidor.expectOne('https://outro-dominio.example/api/dados');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('não anexa token expirado', () => {
    salvarSessao(new Date(Date.now() - 1_000));
    configurar();

    http.get('/api/reservas').subscribe();

    const req = servidor.expectOne('/api/reservas');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('em 401 da API, encerra a sessão e leva ao login', () => {
    salvarSessao(new Date(Date.now() + 60_000));
    configurar();

    http.get('/api/reservas').subscribe({ error: () => {} });
    servidor.expectOne('/api/reservas').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(TestBed.inject(Sessao).estaAutenticado()).toBe(false);
    expect(sessionStorage.getItem(CHAVE)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], expect.objectContaining({
      queryParams: expect.objectContaining({ motivo: 'expirada' }),
    }));
  });

  it('em 401 do próprio login (senha errada), não redireciona', () => {
    configurar();

    http.post('/api/auth/login', {}).subscribe({ error: () => {} });
    servidor.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
  });
});
