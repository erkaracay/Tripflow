export type Tour = {
  id: string
  name: string
  startDate: string
  endDate: string
  guideUserId?: string | null
}

export type TourListItem = {
  id: string
  name: string
  startDate: string
  endDate: string
  arrivedCount: number
  totalCount: number
  guideUserId?: string | null
}

export type UserListItem = {
  id: string
  email: string
  fullName?: string | null
  role: string
}

export type Organization = {
  id: string
  name: string
  slug: string
}

export type Participant = {
  id: string
  fullName: string
  email?: string | null
  phone?: string | null
  checkInCode: string
  arrived: boolean
}

export type MeetingInfo = {
  time: string
  place: string
  mapsUrl: string
  note: string
}

export type LinkInfo = {
  label: string
  url: string
}

export type DayPlan = {
  day: number
  title: string
  items: string[]
}

export type TourPortalInfo = {
  meeting: MeetingInfo
  links: LinkInfo[]
  days: DayPlan[]
  notes: string[]
}

export type CheckInSummary = {
  arrivedCount: number
  totalCount: number
}

export type CheckInResponse = {
  participantId: string
  participantName: string
  alreadyArrived: boolean
  arrivedCount: number
  totalCount: number
}

export type CheckInUndoResponse = {
  participantId: string
  alreadyUndone: boolean
  arrivedCount: number
  totalCount: number
}

export type UserRole = 'SuperAdmin' | 'AgencyAdmin' | 'Guide'

export type LoginResponse = {
  accessToken: string
  role: UserRole
  userId: string
  fullName?: string | null
}
