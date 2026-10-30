import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Resumo {
  id: number;
  nome: string;
}

export interface Sala extends Resumo {
  capacidade: number;
}

export interface Reserva {
  id: number;
  titulo: string;
  sala: Resumo;
  responsavel: Resumo;
  /** ISO 8601 com fuso (UTC). */
  inicio: string;
  fim: string;
  status: 'Ativa' | 'Cancelada';
  /** Calculados pelo backend com as regras do domínio; só orientam a interface. */
  podeEditar: boolean;
  podeCancelar: boolean;
}

export interface DadosReserva {
  salaId: number;
  titulo: string;
  /** ISO 8601 em UTC (toISOString). */
  inicio: string;
  fim: string;
}

@Injectable({ providedIn: 'root' })
export class ReservasApi {
  private readonly http = inject(HttpClient);

  listar(): Observable<Reserva[]> {
    return this.http.get<Reserva[]>('/api/reservas');
  }

  obter(id: number): Observable<Reserva> {
    return this.http.get<Reserva>(`/api/reservas/${id}`);
  }

  criar(dados: DadosReserva): Observable<Reserva> {
    return this.http.post<Reserva>('/api/reservas', dados);
  }

  editar(id: number, dados: DadosReserva): Observable<Reserva> {
    return this.http.put<Reserva>(`/api/reservas/${id}`, dados);
  }

  cancelar(id: number): Observable<Reserva> {
    return this.http.post<Reserva>(`/api/reservas/${id}/cancelamento`, null);
  }

  listarSalas(): Observable<Sala[]> {
    return this.http.get<Sala[]>('/api/salas');
  }
}
