import type { FlightSegment, ParticipantDetails, ParticipantProfile } from '../types'

export type ParticipantExportSource = {
  id: string
  fullName: string
  tcNo: string
  birthDate: string
  gender: string
  phone?: string | null
  email?: string | null
  details?: ParticipantDetails | null
}

type Direction = 'Arrival' | 'Return'

const toText = (value?: string | null) => value?.trim() ?? ''

const toIntOrEmpty = (value?: number | null) =>
  typeof value === 'number' && Number.isFinite(value) ? value : ''

const segmentHasValue = (segment?: FlightSegment | null) => {
  if (!segment) return false

  return Boolean(
    toText(segment.airline) ||
      toText(segment.departureAirport) ||
      toText(segment.arrivalAirport) ||
      toText(segment.flightCode) ||
      toText(segment.departureDate) ||
      toText(segment.departureTime) ||
      toText(segment.arrivalDate) ||
      toText(segment.arrivalTime) ||
      toText(segment.pnr) ||
      toText(segment.ticketNo) ||
      (typeof segment.baggagePieces === 'number' && segment.baggagePieces > 0) ||
      (typeof segment.baggageTotalKg === 'number' && segment.baggageTotalKg > 0)
  )
}

const normalizeSegments = (segments?: FlightSegment[] | null) =>
  [...(segments ?? [])]
    .filter((segment) => segmentHasValue(segment))
    .sort((a, b) => (a.segmentIndex || 0) - (b.segmentIndex || 0))

const hasLegacyDirectionValue = (details: ParticipantDetails, direction: Direction) => {
  if (direction === 'Arrival') {
    return Boolean(
      toText(details.arrivalAirline) ||
        toText(details.arrivalDepartureAirport) ||
        toText(details.arrivalArrivalAirport) ||
        toText(details.arrivalFlightCode) ||
        toText(details.arrivalFlightDate) ||
        toText(details.arrivalDepartureTime) ||
        toText(details.arrivalArrivalTime) ||
        toText(details.arrivalPnr) ||
        toText(details.arrivalTicketNo ?? details.ticketNo) ||
        (typeof details.arrivalBaggagePieces === 'number' && details.arrivalBaggagePieces > 0) ||
        (typeof details.arrivalBaggageTotalKg === 'number' && details.arrivalBaggageTotalKg > 0)
    )
  }

  return Boolean(
    toText(details.returnAirline) ||
      toText(details.returnDepartureAirport) ||
      toText(details.returnArrivalAirport) ||
      toText(details.returnFlightCode) ||
      toText(details.returnFlightDate) ||
      toText(details.returnDepartureTime) ||
      toText(details.returnArrivalTime) ||
      toText(details.returnPnr) ||
      toText(details.returnTicketNo) ||
      (typeof details.returnBaggagePieces === 'number' && details.returnBaggagePieces > 0) ||
      (typeof details.returnBaggageTotalKg === 'number' && details.returnBaggageTotalKg > 0)
  )
}

const toSegmentRow = (tcNo: string, direction: Direction, segment: FlightSegment, fallbackIndex: number) => [
  toText(tcNo),
  direction,
  segment.segmentIndex && segment.segmentIndex > 0 ? segment.segmentIndex : fallbackIndex,
  toText(segment.departureAirport),
  toText(segment.arrivalAirport),
  toText(segment.flightCode),
  toText(segment.departureDate),
  toText(segment.departureTime),
  toText(segment.airline),
  toText(segment.arrivalDate),
  toText(segment.arrivalTime),
  toText(segment.pnr),
  toText(segment.ticketNo),
  toIntOrEmpty(segment.baggagePieces),
  toIntOrEmpty(segment.baggageTotalKg),
]

const toLegacySegmentRow = (tcNo: string, direction: Direction, details: ParticipantDetails) => {
  if (!hasLegacyDirectionValue(details, direction)) {
    return null
  }

  if (direction === 'Arrival') {
    return [
      toText(tcNo),
      'Arrival',
      1,
      toText(details.arrivalDepartureAirport),
      toText(details.arrivalArrivalAirport),
      toText(details.arrivalFlightCode),
      toText(details.arrivalFlightDate),
      toText(details.arrivalDepartureTime),
      toText(details.arrivalAirline),
      toText(details.arrivalFlightDate),
      toText(details.arrivalArrivalTime),
      toText(details.arrivalPnr),
      toText(details.arrivalTicketNo ?? details.ticketNo),
      toIntOrEmpty(details.arrivalBaggagePieces),
      toIntOrEmpty(details.arrivalBaggageTotalKg),
    ]
  }

  return [
    toText(tcNo),
    'Return',
    1,
    toText(details.returnDepartureAirport),
    toText(details.returnArrivalAirport),
    toText(details.returnFlightCode),
    toText(details.returnFlightDate),
    toText(details.returnDepartureTime),
    toText(details.returnAirline),
    toText(details.returnFlightDate),
    toText(details.returnArrivalTime),
    toText(details.returnPnr),
    toText(details.returnTicketNo),
    toIntOrEmpty(details.returnBaggagePieces),
    toIntOrEmpty(details.returnBaggageTotalKg),
  ]
}

const pushDirectionRows = (
  rows: Array<Array<string | number>>,
  tcNo: string,
  direction: Direction,
  segments?: FlightSegment[] | null,
  details?: ParticipantDetails | null
) => {
  const normalized = normalizeSegments(segments)
  if (normalized.length > 0) {
    normalized.forEach((segment, index) => {
      rows.push(toSegmentRow(tcNo, direction, segment, index + 1))
    })
    return
  }

  if (!details) {
    return
  }

  const legacyRow = toLegacySegmentRow(tcNo, direction, details)
  if (legacyRow) {
    rows.push(legacyRow)
  }
}

export const PARTICIPANTS_SHEET_HEADERS = [
  'room_no',
  'room_type',
  'board_type',
  'person_no',
  'agency_name',
  'city',
  'full_name',
  'birth_date',
  'tc_no',
  'gender',
  'phone',
  'email',
  'flight_city',
  'hotel_check_in_date',
  'hotel_check_out_date',
  'insurance_company_name',
  'insurance_policy_no',
  'insurance_start_date',
  'insurance_end_date',
  'arrival_transfer_pickup_time',
  'arrival_transfer_pickup_place',
  'arrival_transfer_dropoff_place',
  'arrival_transfer_vehicle',
  'arrival_transfer_plate',
  'arrival_transfer_driver_info',
  'arrival_transfer_note',
  'return_transfer_pickup_time',
  'return_transfer_pickup_place',
  'return_transfer_dropoff_place',
  'return_transfer_vehicle',
  'return_transfer_plate',
  'return_transfer_driver_info',
  'return_transfer_note',
]

export const FLIGHT_SEGMENTS_SHEET_HEADERS = [
  'tc_no',
  'direction',
  'segment_index',
  'departure_airport',
  'arrival_airport',
  'flight_code',
  'departure_date',
  'departure_time',
  'airline',
  'arrival_date',
  'arrival_time',
  'pnr',
  'ticket_no',
  'baggage_pieces',
  'baggage_total_kg',
]

export const buildParticipantsSheetRows = (participants: ParticipantExportSource[]) =>
  participants.map((participant) => {
    const details = participant.details ?? {}

    return [
      toText(details.roomNo),
      toText(details.roomType),
      toText(details.boardType),
      toText(details.personNo),
      toText(details.agencyName),
      toText(details.city),
      toText(participant.fullName),
      toText(participant.birthDate),
      toText(participant.tcNo),
      toText(participant.gender),
      toText(participant.phone),
      toText(participant.email),
      toText(details.flightCity),
      toText(details.hotelCheckInDate),
      toText(details.hotelCheckOutDate),
      toText(details.insuranceCompanyName),
      toText(details.insurancePolicyNo),
      toText(details.insuranceStartDate),
      toText(details.insuranceEndDate),
      toText(details.arrivalTransferPickupTime),
      toText(details.arrivalTransferPickupPlace),
      toText(details.arrivalTransferDropoffPlace),
      toText(details.arrivalTransferVehicle),
      toText(details.arrivalTransferPlate),
      toText(details.arrivalTransferDriverInfo),
      toText(details.arrivalTransferNote),
      toText(details.returnTransferPickupTime),
      toText(details.returnTransferPickupPlace),
      toText(details.returnTransferDropoffPlace),
      toText(details.returnTransferVehicle),
      toText(details.returnTransferPlate),
      toText(details.returnTransferDriverInfo),
      toText(details.returnTransferNote),
    ]
  })

export const buildFlightSegmentsSheetRows = (
  participants: ParticipantExportSource[],
  profilesByParticipantId: Map<string, ParticipantProfile | null>
) => {
  const rows: Array<Array<string | number>> = []

  participants.forEach((participant) => {
    const profile = profilesByParticipantId.get(participant.id)
    const details = profile?.details ?? participant.details ?? null

    pushDirectionRows(rows, participant.tcNo, 'Arrival', profile?.arrivalSegments, details)
    pushDirectionRows(rows, participant.tcNo, 'Return', profile?.returnSegments, details)
  })

  return rows
}
