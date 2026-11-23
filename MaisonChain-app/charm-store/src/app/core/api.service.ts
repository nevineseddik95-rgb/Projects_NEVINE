import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  list<T = any>(endpoint: string): Observable<T[]> { return this.http.get<T[]>(`${this.base}/${endpoint}`);}
  get<T = any>(endpoint: string, id: string): Observable<T> { return this.http.get<T>(`${this.base}/${endpoint}/${id}`); }
  create(endpoint: string, body: any): Observable<any> { return this.http.post(`${this.base}/${endpoint}`, body); }
  update(endpoint: string, id: string, body: any): Observable<any> { return this.http.put(`${this.base}/${endpoint}/${id}`, body); }
  remove(endpoint: string, id: string): Observable<any> { return this.http.delete(`${this.base}/${endpoint}/${id}`); }

  /** Sends an image to POST /api/upload and returns { url }. */
  upload(file: File): Observable<{ url: string }> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<{ url: string }>(`${this.base}/upload`, form);
  }

  /** Turns a stored image path (e.g. "/uploads/a.jpg") into a loadable URL. */
  img(path?: string | null): string {
    if (!path) return 'placeholder.svg';
    return /^(https?:|data:)/.test(path) ? path : environment.filesUrl + path;
  }
}
