import { Routes } from '@angular/router';
import { Inventory } from './inventory/inventory';
import { Dashboard } from './dashboard/dashboard';
import { Settings } from './settings/settings';

export const routes: Routes = [
    { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    { path: 'dashboard', component: Dashboard },
    { path: 'inventory', component: Inventory },
    { path: 'settings', component: Settings }];
