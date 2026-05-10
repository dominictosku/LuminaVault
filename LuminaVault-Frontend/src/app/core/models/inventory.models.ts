export interface House { id: number; name: string; description?: string; }

export interface Room {
  id: number; houseId: number; name: string; color: string;
  x: number; z: number; width: number; depth: number; height: number;
}

export type FurnitureKind =
  | 'Cabinet' | 'Drawer' | 'Shelf' | 'Wardrobe' | 'Desk'
  | 'Table' | 'Sofa' | 'Bed' | 'Box' | 'Other';

export const FURNITURE_KINDS: FurnitureKind[] = [
  'Cabinet','Drawer','Shelf','Wardrobe','Desk','Table','Sofa','Bed','Box','Other'
];

export interface Furniture {
  id: number; roomId: number; name: string; kind: FurnitureKind;
  x: number; y: number; z: number;
  width: number; depth: number; height: number;
  rotationY: number;
  itemCount: number; containerCount: number;
}

export interface Container {
  id: number; furnitureId: number; name: string;
  description?: string; itemCount: number;
}

export interface ItemPhoto { id: number; url: string; contentType: string; }

export interface DocumentAttachment {
  id: number;
  originalFileName: string;
  url: string;
  contentType: string;
  size: number;
  uploadedAt: string;
}

export interface Item {
  id: number;
  name: string;
  category?: string | null;
  description?: string;
  brand?: string;
  model?: string;
  serialNumber?: string;
  value?: number;
  purchaseDate?: string;
  warrantyUntil?: string;
  quantity: number;
  notes?: string;
  tags: string[];
  furnitureId?: number;
  furnitureName?: string;
  containerId?: number;
  containerName?: string;
  roomId?: number;
  roomName?: string;
  createdAt: string;
  updatedAt: string;
  photos: ItemPhoto[];
  attachments: DocumentAttachment[];
  modelUrl?: string | null;
}

export interface ItemInput {
  name: string;
  category?: string | null;
  description?: string | null;
  brand?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  value?: number | null;
  purchaseDate?: string | null;
  warrantyUntil?: string | null;
  quantity: number;
  notes?: string | null;
  tags: string[];
  roomId?: number | null;
  furnitureId?: number | null;
  containerId?: number | null;
}

export interface StatsSummary {
  totalItems: number;
  totalValue: number;
  byRoom: { roomId: number; roomName: string; count: number }[];
  recent: { id: number; name: string; createdAt: string }[];
}
