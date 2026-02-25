import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AvailabilityStatus } from '../../types';

const mockUseAvailability = vi.fn();
vi.mock('../../hooks/useAvailability', () => ({
  useAvailability: () => mockUseAvailability(),
}));

// Import after mock setup
const { AvailabilityGrid } =
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  await import('../AvailabilityGrid');

function buildDates(): string[] {
  const dates: string[] = [];
  const start = new Date(2025, 0, 6); // Monday Jan 6 2025
  for (let i = 0; i < 14; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    dates.push(d.toISOString().split('T')[0]);
  }
  return dates;
}

function defaultAvailability(): Record<string, AvailabilityStatus> {
  const avail: Record<string, AvailabilityStatus> = {};
  for (const d of buildDates()) {
    avail[d] = AvailabilityStatus.Available;
  }
  return avail;
}

describe('AvailabilityGrid', () => {
  const setDayAvailability = vi.fn();
  const saveAll = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAvailability.mockReturnValue({
      availability: defaultAvailability(),
      setDayAvailability,
      saveAll,
      isLoading: false,
      isSaving: false,
      error: null,
      windowStart: '2025-01-06',
      windowEnd: '2025-01-19',
      dates: buildDates(),
    });
  });

  it('renders 14 day rows', () => {
    render(<AvailabilityGrid />);
    const selects = screen.getAllByRole('combobox');
    expect(selects).toHaveLength(14);
  });

  it('shows loading state', () => {
    mockUseAvailability.mockReturnValue({
      availability: {},
      setDayAvailability,
      saveAll,
      isLoading: true,
      isSaving: false,
      error: null,
      windowStart: '2025-01-06',
      windowEnd: '2025-01-19',
      dates: [],
    });
    render(<AvailabilityGrid />);
    expect(screen.getByText(/loading availability/i)).toBeInTheDocument();
  });

  it('changes dropdown value and calls setDayAvailability', async () => {
    const user = userEvent.setup();
    render(<AvailabilityGrid />);
    const firstSelect = screen.getAllByRole('combobox')[0];
    await user.selectOptions(firstSelect, 'NotAvailable');
    expect(setDayAvailability).toHaveBeenCalledWith(
      '2025-01-06',
      'NotAvailable',
    );
  });

  it('shows save button and calls saveAll on click', async () => {
    const user = userEvent.setup();
    saveAll.mockResolvedValue(true);
    render(<AvailabilityGrid />);
    const saveBtn = screen.getByRole('button', { name: /save availability/i });
    expect(saveBtn).toBeInTheDocument();
    await user.click(saveBtn);
    expect(saveAll).toHaveBeenCalled();
  });

  it('shows success toast after save', async () => {
    const user = userEvent.setup();
    saveAll.mockResolvedValue(true);
    render(<AvailabilityGrid />);
    await user.click(
      screen.getByRole('button', { name: /save availability/i }),
    );
    expect(await screen.findByText(/availability saved/i)).toBeInTheDocument();
  });

  it('shows error toast on save failure', async () => {
    const user = userEvent.setup();
    saveAll.mockResolvedValue(false);
    render(<AvailabilityGrid />);
    await user.click(
      screen.getByRole('button', { name: /save availability/i }),
    );
    expect(
      await screen.findByText(/failed to save/i),
    ).toBeInTheDocument();
  });
});
