import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuthResponse, AuthStatus, House, Room, Furniture, Container,
  Item, ItemInput, ItemPhoto, StatsSummary, FinanceAccount, FinanceAccountInput,
  FinanceSummary, FinanceTransaction, FinanceTransactionInput, FinanceTransactionKind,
  Subscription, SubscriptionInput, OdsImportResult
} from './models';

export const API_BASE = 'http://localhost:5256';

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);

  // --- Auth ---
  authStatus(): Observable<AuthStatus> {
    return this.http.get<AuthStatus>(`${API_BASE}/api/auth/status`);
  }
  login(username: string, password: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/login`, { username, password });
  }
  register(username: string, password: string) {
    return this.http.post<AuthResponse>(`${API_BASE}/api/auth/register`, { username, password });
  }

  // --- Houses ---
  listHouses() { return this.http.get<House[]>(`${API_BASE}/api/houses`); }
  createHouse(name: string, description?: string) {
    return this.http.post<House>(`${API_BASE}/api/houses`, { name, description });
  }
  updateHouse(id: number, name: string, description?: string) {
    return this.http.put<House>(`${API_BASE}/api/houses/${id}`, { name, description });
  }
  deleteHouse(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/houses/${id}`);
  }

  // --- Rooms ---
  listRooms(houseId: number) {
    return this.http.get<Room[]>(`${API_BASE}/api/houses/${houseId}/rooms`);
  }
  createRoom(houseId: number, room: Omit<Room, 'id' | 'houseId'>) {
    return this.http.post<Room>(`${API_BASE}/api/houses/${houseId}/rooms`, room);
  }
  updateRoom(id: number, room: Omit<Room, 'id' | 'houseId'>) {
    return this.http.put<Room>(`${API_BASE}/api/rooms/${id}`, room);
  }
  deleteRoom(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/rooms/${id}`);
  }

  // --- Furniture ---
  listFurniture(roomId: number) {
    return this.http.get<Furniture[]>(`${API_BASE}/api/rooms/${roomId}/furniture`);
  }
  createFurniture(roomId: number, f: Omit<Furniture, 'id' | 'roomId' | 'itemCount' | 'containerCount'>) {
    return this.http.post<Furniture>(`${API_BASE}/api/rooms/${roomId}/furniture`, f);
  }
  updateFurniture(id: number, f: Omit<Furniture, 'id' | 'roomId' | 'itemCount' | 'containerCount'>) {
    return this.http.put<Furniture>(`${API_BASE}/api/furniture/${id}`, f);
  }
  deleteFurniture(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/furniture/${id}`);
  }

  // --- Containers ---
  listContainers(furnitureId: number) {
    return this.http.get<Container[]>(`${API_BASE}/api/furniture/${furnitureId}/containers`);
  }
  createContainer(furnitureId: number, name: string, description?: string) {
    return this.http.post<Container>(`${API_BASE}/api/furniture/${furnitureId}/containers`, { name, description });
  }
  updateContainer(id: number, name: string, description?: string) {
    return this.http.put<Container>(`${API_BASE}/api/containers/${id}`, { name, description });
  }
  deleteContainer(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/containers/${id}`);
  }

  // --- Items ---
  listItems(opts: { q?: string; furnitureId?: number; containerId?: number; roomId?: number } = {}) {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.furnitureId) params = params.set('furnitureId', opts.furnitureId);
    if (opts.containerId) params = params.set('containerId', opts.containerId);
    if (opts.roomId) params = params.set('roomId', opts.roomId);
    return this.http.get<Item[]>(`${API_BASE}/api/items`, { params });
  }
  getItem(id: number) {
    return this.http.get<Item>(`${API_BASE}/api/items/${id}`);
  }
  createItem(input: ItemInput) {
    return this.http.post<Item>(`${API_BASE}/api/items`, input);
  }
  updateItem(id: number, input: ItemInput) {
    return this.http.put<Item>(`${API_BASE}/api/items/${id}`, input);
  }
  deleteItem(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/items/${id}`);
  }
  uploadItemPhoto(itemId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<ItemPhoto>(`${API_BASE}/api/items/${itemId}/photos`, fd);
  }
  deletePhoto(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/photos/${id}`);
  }
  uploadItemModel(itemId: number, file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<{ modelUrl: string }>(`${API_BASE}/api/items/${itemId}/model`, fd);
  }
  deleteItemModel(itemId: number) {
    return this.http.delete<void>(`${API_BASE}/api/items/${itemId}/model`);
  }
  modelUrl(item: { modelUrl?: string | null }) {
    return item.modelUrl ? `${API_BASE}${item.modelUrl}` : null;
  }

  // --- Stats ---
  stats() {
    return this.http.get<StatsSummary>(`${API_BASE}/api/items/stats/summary`);
  }

  photoUrl(p: ItemPhoto) { return `${API_BASE}${p.url}`; }

  // --- Finance ---
  financeSummary() {
    return this.http.get<FinanceSummary>(`${API_BASE}/api/finance/summary`);
  }

  listFinanceAccounts(includeArchived = false) {
    return this.http.get<FinanceAccount[]>(`${API_BASE}/api/finance/accounts`, {
      params: includeArchived ? new HttpParams().set('includeArchived', true) : undefined,
    });
  }
  createFinanceAccount(input: FinanceAccountInput) {
    return this.http.post<FinanceAccount>(`${API_BASE}/api/finance/accounts`, input);
  }
  updateFinanceAccount(id: number, input: FinanceAccountInput) {
    return this.http.put<FinanceAccount>(`${API_BASE}/api/finance/accounts/${id}`, input);
  }
  deleteFinanceAccount(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/accounts/${id}`);
  }

  listFinanceTransactions(opts: {
    q?: string;
    accountId?: number;
    category?: string;
    kind?: FinanceTransactionKind;
    from?: string;
    to?: string;
  } = {}) {
    let params = new HttpParams();
    if (opts.q) params = params.set('q', opts.q);
    if (opts.accountId) params = params.set('accountId', opts.accountId);
    if (opts.category) params = params.set('category', opts.category);
    if (opts.kind) params = params.set('kind', opts.kind);
    if (opts.from) params = params.set('from', opts.from);
    if (opts.to) params = params.set('to', opts.to);
    return this.http.get<FinanceTransaction[]>(`${API_BASE}/api/finance/transactions`, { params });
  }
  createFinanceTransaction(input: FinanceTransactionInput) {
    return this.http.post<FinanceTransaction>(`${API_BASE}/api/finance/transactions`, input);
  }
  updateFinanceTransaction(id: number, input: FinanceTransactionInput) {
    return this.http.put<FinanceTransaction>(`${API_BASE}/api/finance/transactions/${id}`, input);
  }
  deleteFinanceTransaction(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/transactions/${id}`);
  }

  listSubscriptions(includeInactive = false) {
    return this.http.get<Subscription[]>(`${API_BASE}/api/finance/subscriptions`, {
      params: includeInactive ? new HttpParams().set('includeInactive', true) : undefined,
    });
  }
  createSubscription(input: SubscriptionInput) {
    return this.http.post<Subscription>(`${API_BASE}/api/finance/subscriptions`, input);
  }
  updateSubscription(id: number, input: SubscriptionInput) {
    return this.http.put<Subscription>(`${API_BASE}/api/finance/subscriptions/${id}`, input);
  }
  deleteSubscription(id: number) {
    return this.http.delete<void>(`${API_BASE}/api/finance/subscriptions/${id}`);
  }

  // --- Import / Export ---
  exportOds() {
    return this.http.get(`${API_BASE}/api/data/export/ods`, {
      observe: 'response',
      responseType: 'blob',
    });
  }

  importOds(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<OdsImportResult>(`${API_BASE}/api/data/import/ods`, fd);
  }
}
