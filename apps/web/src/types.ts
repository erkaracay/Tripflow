export type Event = {
  id: string
  name: string
  startDate: string
  endDate: string
  guideUserId?: string | null
  eventAccessCode?: string | null
  isDeleted: boolean
}

export type EventListItem = {
  id: string
  name: string
  startDate: string
  endDate: string
  arrivedCount: number
  totalCount: number
  guideUserId?: string | null
  isDeleted: boolean
  eventAccessCode: string
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
  isActive: boolean
  isDeleted: boolean
  createdAt: string
  updatedAt: string
}

export type Participant = {
  id: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  birthDate: string
  gender: ParticipantGender
  checkInCode: string
  arrived: boolean
  details?: ParticipantDetails | null
}

export type ParticipantResolve = {
  id: string
  fullName: string
  arrived: boolean
  checkInCode: string
}

export type ParticipantGender = 'Female' | 'Male' | 'Other'

export type ParticipantDetails = {
  roomNo?: string | null
  roomType?: string | null
  personNo?: string | null
  agencyName?: string | null
  city?: string | null
  flightCity?: string | null
  hotelCheckInDate?: string | null
  hotelCheckOutDate?: string | null
  ticketNo?: string | null
  attendanceStatus?: string | null
  arrivalAirline?: string | null
  arrivalDepartureAirport?: string | null
  arrivalArrivalAirport?: string | null
  arrivalFlightCode?: string | null
  arrivalDepartureTime?: string | null
  arrivalArrivalTime?: string | null
  arrivalPnr?: string | null
  arrivalBaggageAllowance?: string | null
  returnAirline?: string | null
  returnDepartureAirport?: string | null
  returnArrivalAirport?: string | null
  returnFlightCode?: string | null
  returnDepartureTime?: string | null
  returnArrivalTime?: string | null
  returnPnr?: string | null
  returnBaggageAllowance?: string | null
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

export type EventPortalInfo = {
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

export type ResetAllCheckInsResponse = {
  removedCount: number
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

export type PortalLoginResponse = {
  portalSessionToken: string
  expiresAt: string
  eventId: string
  participantId: string
}

export type PortalMeResponse = {
  event: {
    id: string
    name: string
    startDate: string
    endDate: string
  }
  participant: {
    id: string
    fullName: string
    phone: string
    email?: string | null
    tcNo: string
    birthDate: string
    gender: ParticipantGender
    checkInCode: string
  }
  portal: EventPortalInfo
}

export type EventAccessCodeResponse = {
  eventId: string
  eventAccessCode: string
}

export type ParticipantImportError = {
  row: number
  tcNo?: string | null
  message: string
  fields: string[]
}

export type ParticipantImportWarning = {
  row: number
  tcNo?: string | null
  message: string
  code: string
}

export type ParticipantImportReport = {
  totalRows: number
  imported: number
  created: number
  updated: number
  failed: number
  ignoredColumns: string[]
  errors: ParticipantImportError[]
  warnings: ParticipantImportWarning[]
}
