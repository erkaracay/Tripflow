import { ref } from 'vue'

export const portalHeaderName = ref('')
export const portalHeaderLogoUrl = ref('')
export const portalHeaderStartDate = ref('')
export const portalHeaderEndDate = ref('')

export const setPortalHeader = (
  name?: string | null,
  logoUrl?: string | null,
  startDate?: string | null,
  endDate?: string | null
) => {
  portalHeaderName.value = name?.trim() ?? ''
  portalHeaderLogoUrl.value = logoUrl?.trim() ?? ''
  portalHeaderStartDate.value = startDate?.trim() ?? ''
  portalHeaderEndDate.value = endDate?.trim() ?? ''
}

export const clearPortalHeader = () => {
  portalHeaderName.value = ''
  portalHeaderLogoUrl.value = ''
  portalHeaderStartDate.value = ''
  portalHeaderEndDate.value = ''
}
