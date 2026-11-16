import { apiGet, apiPut } from '../../shared/api/apiClient.ts'

/** Mirrors CategoryGroup and CategoryTopic in Magpie.Accounts.Application. */
export interface CategoryTopic {
  id: number
  slug: string
  name: string
}

export interface CategoryGroup {
  id: number
  name: string
  topics: CategoryTopic[]
}

export function fetchCategories(signal?: AbortSignal): Promise<CategoryGroup[]> {
  return apiGet<CategoryGroup[]>('/accounts/categories', signal)
}

export async function fetchMyInterests(signal?: AbortSignal): Promise<number[]> {
  const response = await apiGet<{ categoryIds: number[] }>('/accounts/me/interests', signal)
  return response.categoryIds
}

export function saveMyInterests(categoryIds: number[]): Promise<void> {
  return apiPut('/accounts/me/interests', { categoryIds })
}
