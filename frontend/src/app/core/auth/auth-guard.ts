import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Sessao } from './sessao';

/** Exige sessão válida (inclusive não expirada); senão, leva ao login guardando a URL pedida. */
export const authGuard: CanActivateFn = (_rota, estado) => {
  const sessao = inject(Sessao);
  if (sessao.estaAutenticado()) return true;

  sessao.encerrar();
  return inject(Router).createUrlTree(['/login'], { queryParams: { retorno: estado.url } });
};
