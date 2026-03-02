import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Worker } from '../../types';

const mockGetAdminWorkers = vi.fn();
const mockAddWorker = vi.fn();
const mockDeactivateWorker = vi.fn();
const mockActivateWorker = vi.fn();
const mockSetWorkerAdmin = vi.fn();
const mockDeleteWorker = vi.fn();
const mockTriggerScheduleGeneration = vi.fn();
const mockGetBarnConfigs = vi.fn();
const mockSetBarnConfig = vi.fn();
const mockGetBlackouts = vi.fn();
const mockAddBlackout = vi.fn();
const mockDeleteBlackout = vi.fn();

vi.mock('../../services/api', () => ({
  getAdminWorkers: (...args: unknown[]) => mockGetAdminWorkers(...args),
  addWorker: (...args: unknown[]) => mockAddWorker(...args),
  deactivateWorker: (...args: unknown[]) => mockDeactivateWorker(...args),
  activateWorker: (...args: unknown[]) => mockActivateWorker(...args),
  setWorkerAdmin: (...args: unknown[]) => mockSetWorkerAdmin(...args),
  deleteWorker: (...args: unknown[]) => mockDeleteWorker(...args),
  triggerScheduleGeneration: (...args: unknown[]) => mockTriggerScheduleGeneration(...args),
  getBarnConfigs: (...args: unknown[]) => mockGetBarnConfigs(...args),
  setBarnConfig: (...args: unknown[]) => mockSetBarnConfig(...args),
  getBlackouts: (...args: unknown[]) => mockGetBlackouts(...args),
  addBlackout: (...args: unknown[]) => mockAddBlackout(...args),
  deleteBlackout: (...args: unknown[]) => mockDeleteBlackout(...args),
}));

const { AdminPage } = await import('../AdminPage');

const sampleWorkers: Worker[] = [
  { id: '1', displayName: 'Alice', email: 'alice@farm.com', isActive: true, isAdmin: true },
  { id: '2', displayName: 'Bob', email: 'bob@farm.com', isActive: true, isAdmin: false },
  { id: '3', displayName: 'Charlie', email: 'charlie@farm.com', isActive: false, isAdmin: false },
];

describe('AdminPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAdminWorkers.mockResolvedValue(sampleWorkers);
    mockAddWorker.mockResolvedValue({ id: '4', displayName: 'Dave', email: 'dave@farm.com', isActive: true, isAdmin: false });
    mockDeactivateWorker.mockResolvedValue(undefined);
    mockActivateWorker.mockResolvedValue(undefined);
    mockSetWorkerAdmin.mockResolvedValue(undefined);
    mockDeleteWorker.mockResolvedValue(undefined);
    mockTriggerScheduleGeneration.mockResolvedValue({ windowStart: '2024-01-15', windowEnd: '2024-01-28', assignments: [] });
    mockGetBarnConfigs.mockResolvedValue([
      { barn: 'Windhover', workersPerShift: 1 },
      { barn: 'York', workersPerShift: 2 },
    ]);
    mockSetBarnConfig.mockResolvedValue(undefined);
    mockGetBlackouts.mockResolvedValue([
      { id: '2024-12-25', date: '2024-12-25', description: 'Christmas Day', barn: null, shift: null },
    ]);
    mockAddBlackout.mockResolvedValue({ id: '2024-11-28', date: '2024-11-28', description: 'Thanksgiving', barn: null, shift: null });
    mockDeleteBlackout.mockResolvedValue(undefined);
  });

  it('shows loading state initially', () => {
    mockGetAdminWorkers.mockReturnValue(new Promise(() => {}));
    mockGetBarnConfigs.mockReturnValue(new Promise(() => {}));
    mockGetBlackouts.mockReturnValue(new Promise(() => {}));
    render(<AdminPage />);
    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('renders worker table', async () => {
    render(<AdminPage />);
    expect(await screen.findByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
    expect(screen.getByText('alice@farm.com')).toBeInTheDocument();
  });

  it('renders status and role badges', async () => {
    render(<AdminPage />);
    await screen.findByText('Alice');
    const activeBadges = screen.getAllByText('Active');
    const inactiveBadges = screen.getAllByText('Inactive');
    expect(activeBadges.length).toBe(2);
    expect(inactiveBadges.length).toBe(1);
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('submits add worker form', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.type(screen.getByPlaceholderText('Display name'), 'Dave');
    await user.type(screen.getByPlaceholderText('Email'), 'dave@farm.com');
    await user.click(screen.getByText('Add Worker'));

    await waitFor(() => {
      expect(mockAddWorker).toHaveBeenCalledWith('Dave', 'dave@farm.com');
    });
  });

  it('toggles active/inactive', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deactivateButtons = screen.getAllByText('Deactivate');
    await user.click(deactivateButtons[0]);

    await waitFor(() => {
      expect(mockDeactivateWorker).toHaveBeenCalledWith('1');
    });
  });

  it('toggles admin role', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const makeAdminButtons = screen.getAllByText('Make Admin');
    await user.click(makeAdminButtons[0]);

    await waitFor(() => {
      expect(mockSetWorkerAdmin).toHaveBeenCalledWith('2', true);
    });
  });

  it('deletes with confirmation', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deleteButtons = screen.getAllByText('Delete');
    await user.click(deleteButtons[0]);

    // Confirm step
    expect(screen.getByText('Confirm')).toBeInTheDocument();
    expect(screen.getByText('Cancel')).toBeInTheDocument();

    await user.click(screen.getByText('Confirm'));

    await waitFor(() => {
      expect(mockDeleteWorker).toHaveBeenCalledWith('1');
    });
  });

  it('cancels delete', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deleteButtons = screen.getAllByText('Delete');
    await user.click(deleteButtons[0]);
    await user.click(screen.getByText('Cancel'));

    expect(mockDeleteWorker).not.toHaveBeenCalled();
    expect(screen.queryByText('Confirm')).not.toBeInTheDocument();
  });

  it('shows error when loading fails', async () => {
    mockGetAdminWorkers.mockRejectedValue(new Error('fail'));
    render(<AdminPage />);
    expect(await screen.findByText('Failed to load data')).toBeInTheDocument();
  });

  it('renders scheduling period dropdown', async () => {
    render(<AdminPage />);
    await screen.findByText('Alice');

    // The schedule section has a select with date range options
    const selects = screen.getAllByRole('combobox');
    // First select is the schedule window, then barn and shift selects for blackouts
    const scheduleSelect = selects[0];
    expect(scheduleSelect).toBeInTheDocument();
    const options = scheduleSelect.querySelectorAll('option');
    expect(options.length).toBe(4);
    // Each option should contain a date range like "YYYY-MM-DD to YYYY-MM-DD"
    expect(options[0].textContent).toMatch(/\d{4}-\d{2}-\d{2} to \d{4}-\d{2}-\d{2}/);
  });

  it('triggers schedule generation with selected window', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.click(screen.getByText('Generate Schedule'));

    await waitFor(() => {
      expect(mockTriggerScheduleGeneration).toHaveBeenCalledWith(expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/));
    });
    expect(await screen.findByText('Schedule generated and published successfully')).toBeInTheDocument();
  });

  it('shows error when schedule generation fails', async () => {
    mockTriggerScheduleGeneration.mockRejectedValue(new Error('fail'));
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.click(screen.getByText('Generate Schedule'));

    expect(await screen.findByText('Failed to generate schedule')).toBeInTheDocument();
  });

  // --- Barn Config Tests ---

  it('renders barn configuration table', async () => {
    render(<AdminPage />);
    await screen.findByText('Barn Configuration');
    // Both barns should appear in the barn config table
    const barnRows = screen.getAllByText('Windhover');
    expect(barnRows.length).toBeGreaterThanOrEqual(1);
    const yorkRows = screen.getAllByText('York');
    expect(yorkRows.length).toBeGreaterThanOrEqual(1);
  });

  it('increments workers per shift', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Barn Configuration');

    // Find the + button for York (second row, which shows 2)
    const plusButtons = screen.getAllByText('+');
    await user.click(plusButtons[1]); // York's + button

    await waitFor(() => {
      expect(mockSetBarnConfig).toHaveBeenCalledWith('York', 3);
    });
  });

  // --- Blackout Date Tests ---

  it('renders existing blackout dates', async () => {
    render(<AdminPage />);
    await screen.findByText('Blackout Dates');
    expect(screen.getByText('Christmas Day')).toBeInTheDocument();
    expect(screen.getByText('2024-12-25')).toBeInTheDocument();
  });

  it('adds a blackout date', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Blackout Dates');

    // Date inputs in jsdom need direct value setting since type="date" behaves differently
    const dateInput = document.querySelector('input[type="date"]') as HTMLInputElement;
    // Simulate setting the date via fireEvent since userEvent.type doesn't work well with date inputs
    await user.clear(dateInput);
    // Use native event to set value
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!.call(dateInput, '2024-11-28');
    dateInput.dispatchEvent(new Event('input', { bubbles: true }));
    dateInput.dispatchEvent(new Event('change', { bubbles: true }));

    const descInput = screen.getByPlaceholderText('Description (e.g., Christmas Day)');
    await user.type(descInput, 'Thanksgiving');

    await user.click(screen.getByText('Add Blackout'));

    await waitFor(() => {
      expect(mockAddBlackout).toHaveBeenCalled();
    });
  });

  it('removes a blackout date', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Christmas Day');

    await user.click(screen.getByText('Remove'));

    await waitFor(() => {
      expect(mockDeleteBlackout).toHaveBeenCalledWith('2024-12-25');
    });
  });
});
