export type Event = {
  id: string
  name: string
  startDate: string
  endDate: string
  logoUrl?: string | null
  guideUserIds: string[]
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
  guideUserIds: string[]
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
  willNotAttend: boolean
  details?: ParticipantDetails | null
  lastLog?: ParticipantLastLog | null
}

export type ParticipantLastLog = {
  direction: 'Entry' | 'Exit'
  method: 'Manual' | 'QrScan'
  result: string
  createdAt: string
}

export type ParticipantWillNotAttendResponse = {
  id: string
  willNotAttend: boolean
  arrived: boolean
  lastLog?: ParticipantLastLog | null
}

export type ActivityParticipantWillNotAttendResponse = {
  id: string
  willNotAttend: boolean
  activityState: ActivityParticipantState
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
  boardType?: string | null
  personNo?: string | null
  agencyName?: string | null
  city?: string | null
  flightCity?: string | null
  hotelCheckInDate?: string | null
  hotelCheckOutDate?: string | null
  ticketNo?: string | null
  arrivalTicketNo?: string | null
  returnTicketNo?: string | null
  attendanceStatus?: string | null
  insuranceCompanyName?: string | null
  insurancePolicyNo?: string | null
  insuranceStartDate?: string | null
  insuranceEndDate?: string | null
  arrivalAirline?: string | null
  arrivalDepartureAirport?: string | null
  arrivalArrivalAirport?: string | null
  arrivalFlightCode?: string | null
  arrivalFlightDate?: string | null
  arrivalDepartureTime?: string | null
  arrivalArrivalTime?: string | null
  arrivalPnr?: string | null
  arrivalBaggageAllowance?: string | null
  arrivalBaggagePieces?: number | null
  arrivalBaggageTotalKg?: number | null
  arrivalCabinBaggage?: string | null
  returnAirline?: string | null
  returnDepartureAirport?: string | null
  returnArrivalAirport?: string | null
  returnFlightCode?: string | null
  returnFlightDate?: string | null
  returnDepartureTime?: string | null
  returnArrivalTime?: string | null
  returnPnr?: string | null
  returnBaggageAllowance?: string | null
  returnBaggagePieces?: number | null
  returnBaggageTotalKg?: number | null
  returnCabinBaggage?: string | null
  arrivalTransferPickupTime?: string | null
  arrivalTransferPickupPlace?: string | null
  arrivalTransferDropoffPlace?: string | null
  arrivalTransferVehicle?: string | null
  arrivalTransferPlate?: string | null
  arrivalTransferDriverInfo?: string | null
  arrivalTransferNote?: string | null
  returnTransferPickupTime?: string | null
  returnTransferPickupPlace?: string | null
  returnTransferDropoffPlace?: string | null
  returnTransferVehicle?: string | null
  returnTransferPlate?: string | null
  returnTransferDriverInfo?: string | null
  returnTransferNote?: string | null
}

export type ParticipantTableItem = {
  id: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  birthDate: string
  gender: string
  checkInCode: string
  arrived: boolean
  arrivedAt?: string | null
  details?: ParticipantDetails | null
}

export type ParticipantTableResponse = {
  page: number
  pageSize: number
  total: number
  items: ParticipantTableItem[]
}

export type ParticipantProfile = {
  id: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  birthDate: string
  gender: ParticipantGender
  checkInCode: string
  arrived: boolean
  arrivedAt?: string | null
  tcNoDuplicate: boolean
  details?: ParticipantDetails | null
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

export type EventDay = {
  id: string
  date: string
  title?: string | null
  notes?: string | null
  placesToVisit?: string | null
  sortOrder: number
  isActive: boolean
  activityCount: number
}

export type EventActivity = {
  id: string
  eventDayId: string
  title: string
  type: string
  startTime?: string | null
  endTime?: string | null
  locationName?: string | null
  address?: string | null
  directions?: string | null
  notes?: string | null
  checkInEnabled: boolean
  requiresCheckIn: boolean
  checkInMode: string
  menuText?: string | null
  programContent?: string | null
  surveyUrl?: string | null
}

export type ActivityCheckInResponse = {
  participantId: string
  participantName: string
  result: string
  direction: string
  method: string
  loggedAt: string
}

export type ActivityLastLog = {
  direction: string
  method: string
  result: string
  createdAt: string
}

export type ActivityParticipantState = {
  isCheckedIn: boolean
  willNotAttend: boolean
  lastLog?: ActivityLastLog | null
}

export type ActivityParticipantTableItem = {
  id: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  checkInCode: string
  roomNo?: string | null
  agencyName?: string | null
  activityState: ActivityParticipantState
}

export type ActivityParticipantTableResponse = {
  page: number
  pageSize: number
  total: number
  items: ActivityParticipantTableItem[]
}

export type EventItem = {
  id: string
  type: string
  title: string
  name: string
  isActive: boolean
  sortOrder: number
}

export type ItemActionResponse = {
  participantId: string
  participantName: string
  result: string
  action: string
  method: string
  loggedAt: string
}

export type ItemLastLog = {
  action: string
  method: string
  result: string
  createdAt: string
}

export type ItemParticipantState = {
  given: boolean
  lastLog?: ItemLastLog | null
}

export type ItemParticipantTableItem = {
  id: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  checkInCode: string
  roomNo?: string | null
  agencyName?: string | null
  itemState: ItemParticipantState
}

export type ItemParticipantTableResponse = {
  page: number
  pageSize: number
  total: number
  items: ItemParticipantTableItem[]
}

export type EventScheduleDay = {
  id: string
  date: string
  title?: string | null
  notes?: string | null
  placesToVisit?: string | null
  sortOrder: number
  isActive: boolean
  activities: EventActivity[]
}

export type EventSchedule = {
  days: EventScheduleDay[]
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
  direction?: 'Entry' | 'Exit' | null
  loggedAt?: string | null
  result?: string | null
}

export type CheckInUndoResponse = {
  participantId: string
  alreadyUndone: boolean
  arrivedCount: number
  totalCount: number
}

export type EventParticipantLogItem = {
  id: string
  createdAt: string
  direction: string
  method: string
  result: string
  participantId?: string | null
  participantName?: string | null
  participantTcNo?: string | null
  participantPhone?: string | null
  checkInCode?: string | null
  actorUserId?: string | null
  actorEmail?: string | null
  actorRole?: string | null
  ipAddress?: string | null
  userAgent?: string | null
}

export type EventParticipantLogListResponse = {
  page: number
  pageSize: number
  total: number
  items: EventParticipantLogItem[]
}

export type ResetAllActivityCheckInsResponse = {
  removedCount: number
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

export type PortalResolveEventResponse = {
  eventId: string
  eventTitle: string
}

export type PortalDocTabDto = {
  id: string
  title: string
  type: string
  sortOrder: number
  content: unknown
}

export type PortalFlightInfo = {
  airline?: string | null
  departureAirport?: string | null
  arrivalAirport?: string | null
  flightCode?: string | null
  ticketNo?: string | null
  date?: string | null
  departureTime?: string | null
  arrivalTime?: string | null
  pnr?: string | null
  baggagePieces?: number | null
  baggageTotalKg?: number | null
  cabinBaggage?: string | null
}

export type PortalInsuranceInfo = {
  companyName?: string | null
  policyNo?: string | null
  startDate?: string | null
  endDate?: string | null
}

export type PortalTransferInfo = {
  pickupTime?: string | null
  pickupPlace?: string | null
  dropoffPlace?: string | null
  vehicle?: string | null
  plate?: string | null
  driverInfo?: string | null
  note?: string | null
}

export type PortalParticipantTravel = {
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  hotelCheckInDate?: string | null
  hotelCheckOutDate?: string | null
  ticketNo?: string | null
  arrivalBaggageAllowance?: string | null
  returnBaggageAllowance?: string | null
  arrival?: PortalFlightInfo | null
  return?: PortalFlightInfo | null
  transferOutbound?: PortalTransferInfo | null
  transferReturn?: PortalTransferInfo | null
  insurance?: PortalInsuranceInfo | null
}

export type PortalDocsResponse = {
  tabs: PortalDocTabDto[]
  participantTravel: PortalParticipantTravel
}

export type EventDocTabDto = {
  id: string
  eventId: string
  title: string
  type: string
  sortOrder: number
  isActive: boolean
  content: unknown
}

export type PortalMeResponse = {
  event: {
    id: string
    name: string
    startDate: string
    endDate: string
    logoUrl?: string | null
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
  schedule: EventSchedule
  docs: PortalDocsResponse
}

export type EventAccessCodeResponse = {
  eventId: string
  eventAccessCode: string
}

export type ParticipantImportError = {
  row: number
  rowIndex?: number
  tcNo?: string | null
  field?: string | null
  code?: string | null
  message: string
  fields: string[]
}

export type ParticipantImportWarning = {
  row: number
  rowIndex?: number
  tcNo?: string | null
  field?: string | null
  message: string
  code: string
}

export type ParticipantImportPreviewRow = {
  rowIndex: number
  fullName?: string | null
  phone?: string | null
  tcNo?: string | null
  birthDate?: string | null
  gender?: string | null
  hotelCheckInDate?: string | null
  hotelCheckOutDate?: string | null
  arrivalDepartureTime?: string | null
  arrivalArrivalTime?: string | null
  returnDepartureTime?: string | null
  returnArrivalTime?: string | null
  arrivalBaggagePieces?: number | null
  arrivalBaggageTotalKg?: number | null
  returnBaggagePieces?: number | null
  returnBaggageTotalKg?: number | null
}

export type ParticipantImportReport = {
  totalRows: number
  validRows?: number
  imported: number
  created: number
  updated: number
  failed: number
  skipped?: number
  errorCount?: number
  previewLimit?: number
  previewTruncated?: boolean
  ignoredColumns: string[]
  errors: ParticipantImportError[]
  warnings: ParticipantImportWarning[]
  parsedPreviewRows?: ParticipantImportPreviewRow[]
}
