import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi } from 'vitest'
import * as scheduleApi from '../../../src/frontend/src/api/schedule'
import DashboardPage from '../../../src/frontend/src/pages/DashboardPage'

const mockItem: scheduleApi.ScheduleItem = {
  logId: '1', scheduledTime: new Date().toISOString(), scheduledTimeLocal: '08:00',
  status: 'pending', skippedBy: null,
  patient: { id: 'p1', name: 'João' },
  medication: { id: 'm1', name: 'Losartana', dosage: '50', unit: 'mg', applicationMethod: 'oral' }
}

describe('DashboardPage', () => {
  it('renders schedule items', async () => {
    vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    await waitFor(() => expect(screen.getByText(/Losartana/)).toBeInTheDocument())
    expect(screen.getByText(/João/)).toBeInTheDocument()
  })

  it('confirm button calls confirmLog', async () => {
    vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])
    const confirmSpy = vi.spyOn(scheduleApi, 'confirmLog').mockResolvedValue({ id: '1', status: 'taken' })

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    await waitFor(() => screen.getByRole('button', { name: /confirm/i }))
    fireEvent.click(screen.getByRole('button', { name: /confirm/i }))

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledWith('1'))
  })

  it('retries queued confirm when online event fires', async () => {
    vi.spyOn(scheduleApi, 'getTodaySchedule').mockResolvedValue([mockItem])
    // First call fails (offline), retry succeeds
    const confirmSpy = vi.spyOn(scheduleApi, 'confirmLog')
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce({ id: '1', status: 'taken' })

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)
    await waitFor(() => screen.getByRole('button', { name: /confirm/i }))

    // Click while "offline" → queues the action
    fireEvent.click(screen.getByRole('button', { name: /confirm/i }))
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument())

    // Simulate coming back online → retry fires
    window.dispatchEvent(new Event('online'))

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledTimes(2))
  })
})
