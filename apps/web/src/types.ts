export type Tour = {
  id: string
  name: string
  startDate: string
  endDate: string
}

export type Participant = {
  id: string
  fullName: string
  email?: string | null
  phone?: string | null
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
