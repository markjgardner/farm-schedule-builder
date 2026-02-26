export enum Barn {
  Windhover = 'Windhover',
  York = 'York',
}

export enum ShiftTime {
  Morning = 'Morning',
  Evening = 'Evening',
}

export enum AvailabilityStatus {
  Available = 'Available',
  NotAvailable = 'NotAvailable',
  MorningOnly = 'MorningOnly',
  EveningOnly = 'EveningOnly',
}

export interface Worker {
  id: string;
  displayName: string;
  email: string;
  isActive: boolean;
  isAdmin: boolean;
}

export interface Availability {
  workerId: string;
  date: string;
  status: AvailabilityStatus;
}

export interface ShiftAssignment {
  date: string;
  barn: Barn;
  shift: ShiftTime;
  workerId: string;
  workerName: string;
}

export interface Schedule {
  windowStart: string;
  windowEnd: string;
  generatedAt: string;
  assignments: ShiftAssignment[];
}

export interface ClientPrincipal {
  identityProvider: string;
  userId: string;
  userDetails: string;
  userRoles: string[];
}
