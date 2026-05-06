import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login').then(m => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then(m => m.ShellComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./pages/dashboard/dashboard').then(m => m.DashboardComponent),
      },
      {
        path: 'items',
        loadComponent: () => import('./pages/items/items').then(m => m.ItemsComponent),
      },
      {
        path: 'items/new',
        loadComponent: () => import('./pages/items/item-form').then(m => m.ItemFormComponent),
      },
      {
        path: 'items/:id',
        loadComponent: () => import('./pages/items/item-form').then(m => m.ItemFormComponent),
      },
      {
        path: 'planner',
        loadComponent: () => import('./pages/planner/planner').then(m => m.PlannerComponent),
      },
      {
        path: 'rooms',
        loadComponent: () => import('./pages/rooms/rooms').then(m => m.RoomsComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
