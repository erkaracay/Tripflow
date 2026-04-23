import type {
  AccommodationSegment,
  AccommodationSegmentParticipantTableItem,
  FlightSegment,
  ParticipantDetails,
  ParticipantProfile,
} from '../types'

export type ParticipantExportSource = {
  id: string
  firstName: string
  lastName: string
  fullName?: string | null
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
      (typeof segment.baggageTotalKg === 'number' && segment.baggageTotalKg > 0) ||
      toText(segment.cabinBaggage)
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
        (typeof details.arrivalBaggageTotalKg === 'number' && details.arrivalBaggageTotalKg > 0) ||
        toText(details.arrivalCabinBaggage)
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
      (typeof details.returnBaggageTotalKg === 'number' && details.returnBaggageTotalKg > 0) ||
      toText(details.returnCabinBaggage)
  )
}

const buildParticipantName = (participant: ParticipantExportSource) =>
  toText(participant.fullName) || [toText(participant.firstName), toText(participant.lastName)].filter(Boolean).join(' ')

const toSegmentRow = (
  participant: ParticipantExportSource,
  direction: Direction,
  segment: FlightSegment,
  fallbackIndex: number
) => [
  toText(participant.tcNo),
  direction,
  segment.segmentIndex && segment.segmentIndex > 0 ? segment.segmentIndex : fallbackIndex,
  toText(segment.departureAirport),
  toText(segment.arrivalAirport),
  toText(segment.flightCode),
  toText(segment.departureDate),
  toText(segment.departureTime),
  buildParticipantName(participant),
  toText(segment.airline),
  toText(segment.arrivalDate),
  toText(segment.arrivalTime),
  toText(segment.pnr),
  toText(segment.ticketNo),
  toIntOrEmpty(segment.baggagePieces),
  toIntOrEmpty(segment.baggageTotalKg),
  toText(segment.cabinBaggage),
]

const toLegacySegmentRow = (
  participant: ParticipantExportSource,
  direction: Direction,
  details: ParticipantDetails
) => {
  if (!hasLegacyDirectionValue(details, direction)) {
    return null
  }

  if (direction === 'Arrival') {
    return [
      toText(participant.tcNo),
      'Arrival',
      1,
      toText(details.arrivalDepartureAirport),
      toText(details.arrivalArrivalAirport),
      toText(details.arrivalFlightCode),
      toText(details.arrivalFlightDate),
      toText(details.arrivalDepartureTime),
      buildParticipantName(participant),
      toText(details.arrivalAirline),
      toText(details.arrivalFlightDate),
      toText(details.arrivalArrivalTime),
      toText(details.arrivalPnr),
      toText(details.arrivalTicketNo ?? details.ticketNo),
      toIntOrEmpty(details.arrivalBaggagePieces),
      toIntOrEmpty(details.arrivalBaggageTotalKg),
      toText(details.arrivalCabinBaggage),
    ]
  }

  return [
    toText(participant.tcNo),
    'Return',
    1,
    toText(details.returnDepartureAirport),
    toText(details.returnArrivalAirport),
    toText(details.returnFlightCode),
    toText(details.returnFlightDate),
    toText(details.returnDepartureTime),
    buildParticipantName(participant),
    toText(details.returnAirline),
    toText(details.returnFlightDate),
    toText(details.returnArrivalTime),
    toText(details.returnPnr),
    toText(details.returnTicketNo),
    toIntOrEmpty(details.returnBaggagePieces),
    toIntOrEmpty(details.returnBaggageTotalKg),
    toText(details.returnCabinBaggage),
  ]
}

const pushDirectionRows = (
  rows: Array<Array<string | number>>,
  participant: ParticipantExportSource,
  direction: Direction,
  segments?: FlightSegment[] | null,
  details?: ParticipantDetails | null
) => {
  const normalized = normalizeSegments(segments)
  if (normalized.length > 0) {
    normalized.forEach((segment, index) => {
      rows.push(toSegmentRow(participant, direction, segment, index + 1))
    })
    return
  }

  if (!details) {
    return
  }

  const legacyRow = toLegacySegmentRow(participant, direction, details)
  if (legacyRow) {
    rows.push(legacyRow)
  }
}

export const PARTICIPANTS_SHEET_HEADERS = [
  'agency_name',
  'city',
  'first_name',
  'last_name',
  'birth_date',
  'tc_no',
  'gender',
  'phone',
  'email',
  'flight_city',
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
  'arrival_transfer_seat_no',
  'arrival_transfer_compartment_no',
  'arrival_transfer_note',
  'return_transfer_pickup_time',
  'return_transfer_pickup_place',
  'return_transfer_dropoff_place',
  'return_transfer_vehicle',
  'return_transfer_plate',
  'return_transfer_driver_info',
  'return_transfer_seat_no',
  'return_transfer_compartment_no',
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
  'participant_name',
  'airline',
  'arrival_date',
  'arrival_time',
  'pnr',
  'ticket_no',
  'baggage_pieces',
  'baggage_total_kg',
  'cabin_baggage',
]

export const ACCOMMODATION_SEGMENTS_SHEET_HEADERS = [
  'segment_key',
  'accommodation',
  'start_date',
  'end_date',
]

export const ACCOMMODATION_ASSIGNMENTS_SHEET_HEADERS = [
  'tc_no',
  'segment_key',
  'accommodation_override',
  'room_no',
  'room_type',
  'board_type',
  'person_no',
]

export const buildParticipantsSheetRows = (participants: ParticipantExportSource[]) =>
  participants.map((participant) => {
    const details = participant.details ?? {}

    return [
      toText(details.agencyName),
      toText(details.city),
      toText(participant.firstName),
      toText(participant.lastName),
      toText(participant.birthDate),
      toText(participant.tcNo),
      toText(participant.gender),
      toText(participant.phone),
      toText(participant.email),
      toText(details.flightCity),
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
      toText(details.arrivalTransferSeatNo),
      toText(details.arrivalTransferCompartmentNo),
      toText(details.arrivalTransferNote),
      toText(details.returnTransferPickupTime),
      toText(details.returnTransferPickupPlace),
      toText(details.returnTransferDropoffPlace),
      toText(details.returnTransferVehicle),
      toText(details.returnTransferPlate),
      toText(details.returnTransferDriverInfo),
      toText(details.returnTransferSeatNo),
      toText(details.returnTransferCompartmentNo),
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

    pushDirectionRows(rows, participant, 'Arrival', profile?.arrivalSegments, details)
    pushDirectionRows(rows, participant, 'Return', profile?.returnSegments, details)
  })

  return rows
}

export const buildAccommodationSegmentsSheetRows = (segments: AccommodationSegment[]) => {
  const orderedSegments = [...segments].sort((left, right) => {
    if (left.startDate === right.startDate) {
      return left.sortOrder - right.sortOrder
    }

    return left.startDate.localeCompare(right.startDate)
  })

  const segmentKeyById = new Map<string, string>()
  const rows = orderedSegments.map((segment, index) => {
    const key = `SEGMENT_${String(index + 1).padStart(2, '0')}`
    segmentKeyById.set(segment.id, key)

    return [key, toText(segment.defaultAccommodationTitle), toText(segment.startDate), toText(segment.endDate)]
  })

  return { rows, segmentKeyById }
}

export const buildAccommodationAssignmentsSheetRows = (
  participants: ParticipantExportSource[],
  participantRowsBySegmentId: Map<string, AccommodationSegmentParticipantTableItem[]>,
  segmentKeyById: Map<string, string>
) => {
  const participantById = new Map(participants.map((participant) => [participant.id, participant]))
  const rows: string[][] = []

  for (const [segmentId, segmentKey] of segmentKeyById.entries()) {
    const participantRows = [...(participantRowsBySegmentId.get(segmentId) ?? [])].sort((left, right) => {
      const fullNameCompare = toText(left.fullName).localeCompare(toText(right.fullName), 'tr')
      if (fullNameCompare !== 0) {
        return fullNameCompare
      }

      return toText(left.tcNo).localeCompare(toText(right.tcNo))
    })

    for (const row of participantRows) {
      const participant = participantById.get(row.participantId)
      if (!participant) {
        continue
      }

      const hasExplicitAssignment = Boolean(
        row.usesOverride ||
          toText(row.roomNo) ||
          toText(row.roomType) ||
          toText(row.boardType) ||
          toText(row.personNo)
      )

      if (!hasExplicitAssignment) {
        continue
      }

      rows.push([
        toText(participant.tcNo),
        segmentKey,
        row.usesOverride ? toText(row.effectiveAccommodationTitle) : '',
        toText(row.roomNo),
        toText(row.roomType),
        toText(row.boardType),
        toText(row.personNo),
      ])
    }
  }

  return rows
}

export const buildBulkFlightTemplateSheetRows = (
  participants: ParticipantExportSource[],
  arrivalTemplate?: FlightSegment[] | null,
  returnTemplate?: FlightSegment[] | null
) => {
  const rows: Array<Array<string | number>> = []
  const normalizedArrival = normalizeSegments(arrivalTemplate)
  const normalizedReturn = normalizeSegments(returnTemplate)

  participants.forEach((participant) => {
    normalizedArrival.forEach((segment, index) => {
      rows.push(
        toSegmentRow(
          participant,
          'Arrival',
          {
            ...segment,
            segmentIndex: index + 1,
          },
          index + 1
        )
      )
    })

    normalizedReturn.forEach((segment, index) => {
      rows.push(
        toSegmentRow(
          participant,
          'Return',
          {
            ...segment,
            segmentIndex: index + 1,
          },
          index + 1
        )
      )
    })
  })

  return rows
}
