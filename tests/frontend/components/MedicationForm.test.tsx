import { render, screen, fireEvent } from '@testing-library/react'
import MedicationForm from '../../../src/frontend/src/components/MedicationForm'
import { vi } from 'vitest'

describe('MedicationForm', () => {
  it('derives frequencyPerDay from times array length', () => {
    const onSubmit = vi.fn()
    render(<MedicationForm onSubmit={onSubmit} submitLabel="Save" />)

    // Default is 1 time
    expect(screen.getAllByPlaceholderText(/HH:mm/i)).toHaveLength(1)

    // Change frequency to 3
    fireEvent.change(screen.getByLabelText(/frequency/i), { target: { value: '3' } })
    expect(screen.getAllByPlaceholderText(/HH:mm/i)).toHaveLength(3)
  })

  it('distributes times evenly when frequency changes', () => {
    render(<MedicationForm onSubmit={vi.fn()} submitLabel="Save" />)

    fireEvent.change(screen.getByLabelText(/frequency/i), { target: { value: '3' } })

    const inputs = screen.getAllByPlaceholderText(/HH:mm/i) as HTMLInputElement[]
    expect(inputs[0].value).toBe('08:00')
    expect(inputs[1].value).toBe('16:00')
    expect(inputs[2].value).toBe('00:00')
  })
})
