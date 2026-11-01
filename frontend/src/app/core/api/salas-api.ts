import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface Sala {
  id: number;
  nome: string;
  capacidade: number;
}

@Injectable({ providedIn: 'root' })
export class SalasApi {
  private readonly http = inject(HttpClient);

  listar(): Observable<Sala[]> {
    return this.http.get<Sala[]>('/api/salas');
  }
}
