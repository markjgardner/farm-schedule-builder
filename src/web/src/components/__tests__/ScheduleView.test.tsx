import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Barn, ShiftTime } from '../../types';
import type { Schedule } from '../../types';

const mockGetCurrentSchedule = vi.fn();
vi.mock('../../services/api', () => ({
  getCurrentSchedule: () => mockGetCurrentSchedule(),
}));

const { ScheduleView } = await import('../ScheduleView');

const sampleSchedule: Schedule = {
  windowStart: '2025-01-06',
  windowEnd: '2025-01-19',
  generatedAt: '2025-01-05T12:00:00Z',
  assignments: [
    {
      date: '2025-01-06',
      barn: Barn.Windhover,
      shift: ShiftTime.Morning,
      workerId: '1',
      workerName: 'Alice',
    },
    {
      date: '2025-01-06',
      barn: Barn.Windhover,
      shift: ShiftTime.Evening,
      workerId: '2',
      workerName: 'Bob',
    },
    {
      date: '2025-01-06',
      barn: Barn.York,
      shift: ShiftTime.Morning,
      workerId: '3',
      workerName: 'Charlie',
    },
    {
      date: '2025-01-06',
      barn: Barn.York,
      shift: ShiftTime.Evening,
      workerId: '',
      workerName: 'UNFILLED',
    },
  ],
};

describe('ScheduleView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows no schedule message when empty', async () => {
    mockGetCurrentSchedule.mockResolvedValue(null);
    render(<ScheduleView />);
    expect(
      await screen.findByText(/no schedule available yet/i),
    ).toBeInTheDocument();
  });

  it('renders schedule table with assignments', async () => {
    mockGetCurrentSchedule.mockResolvedValue(sampleSchedule);
    render(<ScheduleView />);
    expect(await screen.findByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('renders column headers', async () => {
    mockGetCurrentSchedule.mockResolvedValue(sampleSchedule);
    render(<ScheduleView />);
    expect(await screen.findByText('Windhover Morning')).toBeInTheDocument();
    expect(screen.getByText('Windhover Evening')).toBeInTheDocument();
    expect(screen.getByText('York Morning')).toBeInTheDocument();
    expect(screen.getByText('York Evening')).toBeInTheDocument();
  });

  it('highlights UNFILLED slots with unfilled class', async () => {
    mockGetCurrentSchedule.mockResolvedValue(sampleSchedule);
    render(<ScheduleView />);
    const unfilled = await screen.findByText('UNFILLED');
    expect(unfilled).toBeInTheDocument();
    expect(unfilled.closest('td')).toHaveClass('unfilled');
  });
});
