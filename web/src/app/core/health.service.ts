import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface HealthResponse {
  status: 'Healthy' | 'Unhealthy';
  database: boolean;
  customers: number;
  accounts: number;
  merchants: number;
  transactions: number;
  alerts: number;
}

@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);

  get(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>('/api/health');
  }
}
