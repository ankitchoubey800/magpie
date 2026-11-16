import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchCategories, fetchMyInterests, saveMyInterests } from './api.ts'

// A "query key" is the name of a drawer in the cache. Two components that use
// the same key share one fetch and one copy of the data.
const keys = {
  categories: ['categories'] as const,
  myInterests: ['me', 'interests'] as const,
}

/** The tree of groups and topics. It almost never changes, so keep it for an hour. */
export function useCategories() {
  return useQuery({
    queryKey: keys.categories,
    queryFn: ({ signal }) => fetchCategories(signal),
    staleTime: 60 * 60 * 1000,
  })
}

/** The ids of the topics the signed-in reader has chosen. */
export function useMyInterests() {
  return useQuery({
    queryKey: keys.myInterests,
    queryFn: ({ signal }) => fetchMyInterests(signal),
  })
}

/** Saves a new choice. On success the cache is updated at once, so every screen shows it. */
export function useSaveInterests() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (categoryIds: number[]) => saveMyInterests(categoryIds),
    onSuccess: (_result, categoryIds) => {
      // We know exactly what the server now holds: what we just sent.
      // Writing it into the cache saves a second trip to fetch it back.
      queryClient.setQueryData(keys.myInterests, [...categoryIds].sort((a, b) => a - b))
    },
  })
}
