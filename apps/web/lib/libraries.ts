import 'server-only';

export type LibraryPlan = {
  id: string;
  code: string;
  name: string;
  price: number;
  cycle: 'MONTHLY' | 'YEARLY';
  active: boolean;
};
export type LibraryDoc = {
  id: string;
  slug: string;
  title: string;
  summary: string;
  group: string;
  position: number;
  markdown: string;
  status: string;
};
export type LibraryRelease = {
  id: string;
  version: string;
  sha256: string;
  sizeBytes: number;
  signaturePresent: boolean;
  createdAt: string;
  publishedAt?: string;
  isActive: boolean;
};
export type Library = {
  id: string;
  slug: string;
  name: string;
  productId: string;
  moduleCode: string;
  catalogId?: string;
  summary: string;
  description: string;
  features: string[];
  requirements: string[];
  status: string;
  imageUrl?: string;
  publishedAt?: string;
  plans: LibraryPlan[];
  docs: LibraryDoc[];
  releases: LibraryRelease[];
};

function baseUrl() {
  const value = process.env.ATLAS_LICENSE_API;
  if (!value) throw new Error('ATLAS_LICENSE_API não configurada no servidor web.');
  return value.replace(/\/$/, '');
}
async function read<T>(path: string): Promise<T> {
  const response = await fetch(`${baseUrl()}${path}`, { cache: 'no-store' });
  if (!response.ok)
    throw new Error(response.status === 404 ? 'not_found' : `library_api_${response.status}`);
  return response.json() as Promise<T>;
}
export const getLibraries = () => read<Library[]>('/api/v1/libraries');
export const getLibrary = (slug: string) =>
  read<Library>(`/api/v1/libraries/${encodeURIComponent(slug)}`);
