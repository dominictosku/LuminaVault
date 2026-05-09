import { Injectable, inject } from '@angular/core';
import { AuthApi } from './data-access/auth-api';
import { DataTransferApi } from './data-access/data-transfer-api';
import { FinanceApi } from './data-access/finance-api';
import { InventoryApi } from './data-access/inventory-api';
import { SettingsApi } from './data-access/settings-api';
import {
  AssetCategoryInput,
  FinanceAccountInput,
  FinanceTransactionInput,
  FinanceTransactionKind,
  FinanceCategoryInput,
  ItemInput,
  MonthlyAccountSummaryInput,
  SubscriptionInput,
} from './models';

export { API_BASE } from './api-base';

@Injectable({ providedIn: 'root' })
export class Api {
  private auth = inject(AuthApi);
  private inventory = inject(InventoryApi);
  private finance = inject(FinanceApi);
  private settings = inject(SettingsApi);
  private dataTransfer = inject(DataTransferApi);

  authStatus() {
    return this.auth.authStatus();
  }

  login(username: string, password: string) {
    return this.auth.login(username, password);
  }

  register(username: string, password: string) {
    return this.auth.register(username, password);
  }

  listHouses() {
    return this.inventory.listHouses();
  }

  createHouse(name: string, description?: string) {
    return this.inventory.createHouse(name, description);
  }

  updateHouse(id: number, name: string, description?: string) {
    return this.inventory.updateHouse(id, name, description);
  }

  deleteHouse(id: number) {
    return this.inventory.deleteHouse(id);
  }

  listRooms(houseId: number) {
    return this.inventory.listRooms(houseId);
  }

  createRoom(houseId: number, room: Parameters<InventoryApi['createRoom']>[1]) {
    return this.inventory.createRoom(houseId, room);
  }

  updateRoom(id: number, room: Parameters<InventoryApi['updateRoom']>[1]) {
    return this.inventory.updateRoom(id, room);
  }

  deleteRoom(id: number) {
    return this.inventory.deleteRoom(id);
  }

  listFurniture(roomId: number) {
    return this.inventory.listFurniture(roomId);
  }

  createFurniture(roomId: number, furniture: Parameters<InventoryApi['createFurniture']>[1]) {
    return this.inventory.createFurniture(roomId, furniture);
  }

  updateFurniture(id: number, furniture: Parameters<InventoryApi['updateFurniture']>[1]) {
    return this.inventory.updateFurniture(id, furniture);
  }

  deleteFurniture(id: number) {
    return this.inventory.deleteFurniture(id);
  }

  listContainers(furnitureId: number) {
    return this.inventory.listContainers(furnitureId);
  }

  createContainer(furnitureId: number, name: string, description?: string) {
    return this.inventory.createContainer(furnitureId, name, description);
  }

  updateContainer(id: number, name: string, description?: string) {
    return this.inventory.updateContainer(id, name, description);
  }

  deleteContainer(id: number) {
    return this.inventory.deleteContainer(id);
  }

  listItems(opts: Parameters<InventoryApi['listItems']>[0] = {}) {
    return this.inventory.listItems(opts);
  }

  getItem(id: number) {
    return this.inventory.getItem(id);
  }

  createItem(input: ItemInput) {
    return this.inventory.createItem(input);
  }

  updateItem(id: number, input: ItemInput) {
    return this.inventory.updateItem(id, input);
  }

  deleteItem(id: number) {
    return this.inventory.deleteItem(id);
  }

  uploadItemPhoto(itemId: number, file: File) {
    return this.inventory.uploadItemPhoto(itemId, file);
  }

  deletePhoto(id: number) {
    return this.inventory.deletePhoto(id);
  }

  uploadItemModel(itemId: number, file: File) {
    return this.inventory.uploadItemModel(itemId, file);
  }

  deleteItemModel(itemId: number) {
    return this.inventory.deleteItemModel(itemId);
  }

  modelUrl(item: { modelUrl?: string | null }) {
    return this.inventory.modelUrl(item);
  }

  stats() {
    return this.inventory.stats();
  }

  photoUrl(photo: Parameters<InventoryApi['photoUrl']>[0]) {
    return this.inventory.photoUrl(photo);
  }

  financeSummary() {
    return this.finance.financeSummary();
  }

  financeStatistics() {
    return this.finance.financeStatistics();
  }

  listFinanceAccounts(includeArchived = false) {
    return this.finance.listFinanceAccounts(includeArchived);
  }

  createFinanceAccount(input: FinanceAccountInput) {
    return this.finance.createFinanceAccount(input);
  }

  updateFinanceAccount(id: number, input: FinanceAccountInput) {
    return this.finance.updateFinanceAccount(id, input);
  }

  deleteFinanceAccount(id: number) {
    return this.finance.deleteFinanceAccount(id);
  }

  listFinanceTransactions(opts: {
    q?: string;
    accountId?: number;
    category?: string;
    kind?: FinanceTransactionKind;
    from?: string;
    to?: string;
  } = {}) {
    return this.finance.listFinanceTransactions(opts);
  }

  createFinanceTransaction(input: FinanceTransactionInput) {
    return this.finance.createFinanceTransaction(input);
  }

  updateFinanceTransaction(id: number, input: FinanceTransactionInput) {
    return this.finance.updateFinanceTransaction(id, input);
  }

  deleteFinanceTransaction(id: number) {
    return this.finance.deleteFinanceTransaction(id);
  }

  listMonthlySummaries(opts: Parameters<FinanceApi['listMonthlySummaries']>[0] = {}) {
    return this.finance.listMonthlySummaries(opts);
  }

  createMonthlySummary(input: MonthlyAccountSummaryInput) {
    return this.finance.createMonthlySummary(input);
  }

  updateMonthlySummary(id: number, input: MonthlyAccountSummaryInput) {
    return this.finance.updateMonthlySummary(id, input);
  }

  deleteMonthlySummary(id: number) {
    return this.finance.deleteMonthlySummary(id);
  }

  listSubscriptions(includeInactive = false) {
    return this.finance.listSubscriptions(includeInactive);
  }

  createSubscription(input: SubscriptionInput) {
    return this.finance.createSubscription(input);
  }

  updateSubscription(id: number, input: SubscriptionInput) {
    return this.finance.updateSubscription(id, input);
  }

  deleteSubscription(id: number) {
    return this.finance.deleteSubscription(id);
  }

  exportOds() {
    return this.dataTransfer.exportOds();
  }

  importOds(file: File) {
    return this.dataTransfer.importOds(file);
  }

  listAssetCategories() {
    return this.settings.listAssetCategories();
  }

  createAssetCategory(input: AssetCategoryInput) {
    return this.settings.createAssetCategory(input);
  }

  updateAssetCategory(id: number, input: AssetCategoryInput) {
    return this.settings.updateAssetCategory(id, input);
  }

  deleteAssetCategory(id: number) {
    return this.settings.deleteAssetCategory(id);
  }

  listFinanceCategories() {
    return this.settings.listFinanceCategories();
  }

  createFinanceCategory(input: FinanceCategoryInput) {
    return this.settings.createFinanceCategory(input);
  }

  updateFinanceCategory(id: number, input: FinanceCategoryInput) {
    return this.settings.updateFinanceCategory(id, input);
  }

  deleteFinanceCategory(id: number) {
    return this.settings.deleteFinanceCategory(id);
  }
}
