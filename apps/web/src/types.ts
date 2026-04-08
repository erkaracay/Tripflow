export type Event = {
  id: string
  name: string
  startDate: string
  endDate: string
  timeZoneId?: string | null
  logoUrl?: string | null
  guideUserIds: string[]
  eventAccessCode?: string | null
  isDeleted: boolean
}

export type AppComboboxValue = string | number

export type AppComboboxOption = {
  value: AppComboboxValue
  label: string
  description?: string | null
  keywords?: string[]
  groupLabel?: string | null
}

export type ScenarioPresetDefaults = {
  dayCount: number
  accommodationCount: number
  participantCount: number
  equipmentTypeCount: number
  activityDensity: 'light' | 'normal' | 'dense'
  mealMode: 'none' | 'breakfast_only' | 'breakfast_and_dinner'
  flightLegMode: 'mixed' | 'direct_only' | 'layover_heavy'
  includeFlights: boolean
  eventCheckInCoveragePercent: number
  mealSelectionCoveragePercent: number
  participantNamingMode: 'random' | 'prefix'
}

export type ScenarioPresetDto = {
  id: 'minimal' | 'balanced' | 'operations_heavy' | 'meal_heavy' | 'flight_heavy' | 'checkin_heavy'
  label: string
  defaults: ScenarioPresetDefaults
}

export type DevToolsCapabilities = {
  generalSeed: boolean
  scenarioEventGenerator: boolean
  presets: ScenarioPresetDto[]
}

export type CreateScenarioEventRequest = {
  name?: string | null
  startDate: string
  dayCount?: number | null
  timeZoneId: string
  preset: ScenarioPresetDto['id']
  activityDensity?: ScenarioPresetDefaults['activityDensity'] | null
  mealMode?: ScenarioPresetDefaults['mealMode'] | null
  flightLegMode?: ScenarioPresetDefaults['flightLegMode'] | null
  accommodationCount?: number | null
  participantCount?: number | null
  equipmentTypeCount?: number | null
  includeFlights?: boolean | null
  mealSelectionCoveragePercent?: number | null
  eventCheckInCoveragePercent?: number | null
  participantNamingMode?: ScenarioPresetDefaults['participantNamingMode'] | null
  participantNamePrefix?: string | null
  randomSeed?: number | null
}

export type ScenarioEventCounts = {
  days: number
  accommodations: number
  activities: number
  mealActivities: number
  participants: number
  equipmentTypes: number
  mealGroups: number
  mealOptions: number
  mealSelections: number
  flightSegments: number
  eventCheckIns: number
}

export type CreateScenarioEventResponse = {
  eventId: string
  name: string
  startDate: string
  endDate: string
  timeZoneId: string
  eventAccessCode: string
  created: ScenarioEventCounts
}

export type DeleteScenarioEventResponse = {
  eventId: string
  deleted: boolean
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
  organizationName?: string | null
}

export type UserListItem = {
  id: string
  email: string
  fullName?: string | null
  role: string
}

export type UserUpsertResponse = {
  user: UserListItem
  action: 'created' | 'attached' | 'already_attached'
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
  firstName: string
  lastName: string
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
  firstName: string
  lastName: string
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
  accommodationDocTabId?: string | null
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

export type FlightSegment = {
  segmentIndex: number
  airline?: string | null
  departureAirport?: string | null
  arrivalAirport?: string | null
  flightCode?: string | null
  departureDate?: string | null
  departureTime?: string | null
  arrivalDate?: string | null
  arrivalTime?: string | null
  pnr?: string | null
  ticketNo?: string | null
  baggagePieces?: number | null
  baggageTotalKg?: number | null
  cabinBaggage?: string | null
}

export type BulkApplyFlightSegmentsSegments = {
  Arrival?: FlightSegment[] | null
  Return?: FlightSegment[] | null
}

export type BulkApplyFlightSegmentsRequest = {
  participantIds: string[]
  applyDirections: Array<'Arrival' | 'Return'>
  segments: BulkApplyFlightSegmentsSegments
  replaceMode: 'ReplaceDirection'
}

export type BulkApplyFlightSegmentsResponse = {
  affectedCount: number
  applied: {
    Arrival?: number | null
    Return?: number | null
  }
}

export type ParticipantRoomFilters = {
  query?: string | null
  status?: 'all' | 'arrived' | 'not_arrived' | null
  accommodationFilter?: string | null
}

export type ParticipantRoomPatch = {
  accommodationDocTabId?: string | null
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
  hotelCheckInDate?: string | null
  hotelCheckOutDate?: string | null
}

export type ParticipantRoomRowUpdate = {
  participantId: string
  tcNo?: string | null
  patch?: ParticipantRoomPatch | null
}

export type BulkApplyParticipantRoomsRequest = {
  scope?: 'manual' | 'filtered' | 'all_event' | null
  participantIds?: string[] | null
  filters?: ParticipantRoomFilters | null
  patch?: ParticipantRoomPatch | null
  overwriteMode?: 'always' | 'only_empty' | null
  rowUpdates?: ParticipantRoomRowUpdate[] | null
}

export type BulkApplyParticipantRoomsError = {
  participantId?: string | null
  tcNo?: string | null
  code: string
  message: string
}

export type BulkApplyParticipantRoomsResponse = {
  affectedCount: number
  updatedCount: number
  skippedCount: number
  notFoundTcNoCount: number
  errors: BulkApplyParticipantRoomsError[]
}

export type AccommodationSegment = {
  id: string
  defaultAccommodationDocTabId: string
  defaultAccommodationTitle: string
  startDate: string
  endDate: string
  sortOrder: number
}

export type UpsertAccommodationSegmentRequest = {
  defaultAccommodationDocTabId?: string | null
  startDate?: string | null
  endDate?: string | null
  sortOrder?: number | null
}

export type AccommodationSegmentParticipantTableItem = {
  participantId: string
  fullName: string
  tcNo: string
  effectiveAccommodationDocTabId: string
  effectiveAccommodationTitle: string
  usesOverride: boolean
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
}

export type AccommodationSegmentParticipantTableResponse = {
  page: number
  pageSize: number
  total: number
  items: AccommodationSegmentParticipantTableItem[]
}

export type AccommodationSegmentParticipantRowUpdate = {
  participantId: string
  accommodationMode?: 'default' | 'override' | null
  overrideAccommodationDocTabId?: string | null
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
}

export type BulkApplyAccommodationSegmentParticipantsRequest = {
  participantIds?: string[] | null
  overwriteMode?: 'always' | 'only_empty' | null
  accommodationMode?: 'keep' | 'default' | 'override' | null
  overrideAccommodationDocTabId?: string | null
  roomNoMode?: 'keep' | 'set' | 'clear' | null
  roomNo?: string | null
  roomTypeMode?: 'keep' | 'set' | 'clear' | null
  roomType?: string | null
  boardTypeMode?: 'keep' | 'set' | 'clear' | null
  boardType?: string | null
  personNoMode?: 'keep' | 'set' | 'clear' | null
  personNo?: string | null
  rowUpdates?: AccommodationSegmentParticipantRowUpdate[] | null
}

export type BulkApplyAccommodationSegmentParticipantsError = {
  participantId: string
  code: string
  message: string
}

export type BulkApplyAccommodationSegmentParticipantsResponse = {
  affectedCount: number
  createdCount: number
  updatedCount: number
  deletedCount: number
  unchangedCount: number
  errors: BulkApplyAccommodationSegmentParticipantsError[]
}

export type FlightPanelHelperDirection = 'Arrival' | 'Return'

export type ParticipantTableItem = {
  id: string
  firstName: string
  lastName: string
  fullName: string
  phone: string
  email?: string | null
  tcNo: string
  birthDate: string
  gender: string
  checkInCode: string
  arrived: boolean
  arrivedAt?: string | null
  hasArrivalSegments?: boolean
  hasReturnSegments?: boolean
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
  firstName: string
  lastName: string
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
  arrivalSegments?: FlightSegment[]
  returnSegments?: FlightSegment[]
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
  eventContacts?: EventContacts | null
}

export type EventContacts = {
  guideName?: string | null
  guidePhone?: string | null
  leaderName?: string | null
  leaderPhone?: string | null
  emergencyPhone?: string | null
  whatsappGroupUrl?: string | null
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

export type MealOption = {
  id: string
  label: string
  sortOrder: number
  isActive: boolean
}

export type MealGroup = {
  id: string
  activityId: string
  title: string
  sortOrder: number
  allowOther: boolean
  allowNote: boolean
  isActive: boolean
  options: MealOption[]
}

export type MealGroupsResponse = {
  activityId: string
  groups: MealGroup[]
}

export type CreateMealGroupPayload = {
  title: string
  sortOrder?: number
  allowOther?: boolean
  allowNote?: boolean
  isActive?: boolean
}

export type UpdateMealGroupPayload = Partial<CreateMealGroupPayload>

export type CreateMealOptionPayload = {
  label: string
  sortOrder?: number
  isActive?: boolean
}

export type UpdateMealOptionPayload = Partial<CreateMealOptionPayload>

export type PortalMealOption = {
  id: string
  label: string
  sortOrder: number
}

export type PortalMealSelection = {
  groupId: string
  optionId?: string | null
  otherText?: string | null
  note?: string | null
}

export type PortalMealGroup = {
  groupId: string
  title: string
  sortOrder: number
  allowOther: boolean
  allowNote: boolean
  options: PortalMealOption[]
  selection?: PortalMealSelection | null
}

export type PortalMealResponse = {
  activityId: string
  groups: PortalMealGroup[]
}

export type PortalMealSelectionUpsertItem = {
  groupId: string
  optionId?: string | null
  otherText?: string | null
  note?: string | null
}

export type PortalMealSelectionsUpsertRequest = {
  selections: PortalMealSelectionUpsertItem[]
}

export type MealSummaryCount = {
  optionId?: string | null
  label: string
  count: number
}

export type MealSummaryGroup = {
  groupId: string
  title: string
  allowOther: boolean
  allowNote: boolean
  counts: MealSummaryCount[]
  noteCount: number
}

export type MealSummaryResponse = {
  activityId: string
  groups: MealSummaryGroup[]
}

export type MealChoiceParticipant = {
  id: string
  fullName: string
  roomNo?: string | null
  phone: string
}

export type MealChoiceListItem = {
  participant: MealChoiceParticipant
  optionId?: string | null
  optionLabel?: string | null
  otherText?: string | null
  note?: string | null
  updatedAt: string
}

export type MealChoiceListResponse = {
  page: number
  pageSize: number
  total: number
  items: MealChoiceListItem[]
}

export type MealShareSummaryCount = { label: string; count: number }

export type MealShareSummaryGroup = { title: string; counts: MealShareSummaryCount[] }

export type MealShareSummarySpecialRequest = {
  participantName: string
  roomNo?: string | null
  otherText?: string | null
  note?: string | null
}

export type MealShareSummaryResponse = {
  activityTitle: string
  groups: MealShareSummaryGroup[]
  specialRequests: MealShareSummarySpecialRequest[]
}

export type MealReportMode = 'admin' | 'guide'

export type MealReportFilterState = {
  q: string
  onlyNotes: boolean
  onlyOther: boolean
  page: number
  pageSize: number
}

export type MealSelectionDraft = {
  selectedKind: 'option' | 'other' | null
  optionId: string | null
  otherText: string
  note: string
  errorKey: string | null
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

export type AuthMeResponse = {
  role: string
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
  arrivalSegments?: FlightSegment[] | null
  returnSegments?: FlightSegment[] | null
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

export type ParticipantAccommodationStay = {
  id: string
  eventAccommodationId: string
  accommodationTitle: string
  accommodationContent: unknown
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
  checkIn?: string | null
  checkOut?: string | null
  nightCount?: number | null
  isCurrent: boolean
  roommates: string[]
}

export type UpsertParticipantAccommodationStayRequest = {
  eventAccommodationId?: string | null
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
  checkIn?: string | null
  checkOut?: string | null
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
  stays: ParticipantAccommodationStay[]
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
  participantNameReference?: string | null
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
  recordType?: string | null
  direction?: string | null
  segmentIndex?: number | null
  departureAirport?: string | null
  arrivalAirport?: string | null
  flightCode?: string | null
  departureDate?: string | null
  departureTime?: string | null
  arrivalDate?: string | null
  arrivalTime?: string | null
  cabinBaggage?: string | null
  segmentKey?: string | null
  accommodationTitle?: string | null
  startDate?: string | null
  endDate?: string | null
  roomNo?: string | null
  roomType?: string | null
  boardType?: string | null
  personNo?: string | null
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
  accommodationSegmentsImported?: number
  accommodationSegmentsCreated?: number
  accommodationSegmentsUpdated?: number
  accommodationAssignmentsImported?: number
  accommodationAssignmentsCreated?: number
  accommodationAssignmentsUpdated?: number
  accommodationAssignmentsDeleted?: number
}
