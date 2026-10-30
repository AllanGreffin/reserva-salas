import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Sessao } from './sessao';

const PREFIXO_API = '/api/';
const URL_LOGIN = '/api/auth/login';

/**
 * Anexa o token só às requisições para a própria API, para não vazá-lo a outros domínios.
 * Um 401 da API (fora o login) significa sessão inválida ou expirada: encerra a sessão e leva
 * ao login, guardando a página atual para voltar depois.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const sessao = inject(Sessao);
  const router = inject(Router);

  const ehApi = req.url.startsWith(PREFIXO_API);
  const token = ehApi ? sessao.token() : null;
  const requisicao = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(requisicao).pipe(
    catchError((erro: unknown) => {
      if (ehApi && req.url !== URL_LOGIN && erro instanceof HttpErrorResponse && erro.status === 401) {
        sessao.encerrar();
        router.navigate(['/login'], { queryParams: { retorno: router.url, motivo: 'expirada' } });
      }
      return throwError(() => erro);
    }),
  );
};
