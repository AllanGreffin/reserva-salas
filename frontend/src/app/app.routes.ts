import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth-guard';
import { Login } from './features/login/login';
import { ReservaForm } from './features/reservas/reserva-form';
import { ReservasLista } from './features/reservas/reservas-lista';

export const routes: Routes = [
  { path: 'login', component: Login, title: 'Entrar · Reserva de Salas' },
  {
    path: 'reservas',
    canActivate: [authGuard],
    children: [
      { path: '', component: ReservasLista, title: 'Reservas · Reserva de Salas' },
      { path: 'nova', component: ReservaForm, title: 'Nova reserva · Reserva de Salas' },
      { path: ':id/editar', component: ReservaForm, title: 'Editar reserva · Reserva de Salas' },
    ],
  },
  { path: '', pathMatch: 'full', redirectTo: 'reservas' },
  { path: '**', redirectTo: 'reservas' },
];
