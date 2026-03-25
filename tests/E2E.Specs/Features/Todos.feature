Feature: Todos E2E

  Background:
    Given the app is running at "http://localhost:5173"
    And I reset data

# ── Existing Scenarios ──────────────────────────────────────

  Scenario: Add, complete, and delete a todo
    Given I open the Todos page
    When I add a todo titled "Buy milk"
    Then I should see "Buy milk" in the list
    When I complete the todo "Buy milk"
    Then the todo "Buy milk" should appear completed
    When I delete the todo "Buy milk"
    Then I should not see "Buy milk" in the list

  @smoke @e2e
  Scenario: Add a todo (happy path)
    Given I open the Todos page
    When I add a todo titled "Buy milk"
    Then I should see "Buy milk" in the list


# ── My Scenarios ────────────────────────────────────────────

# Suggested

  Scenario: Create a todo with priority and due date
    Given I open the Todos page
    When I create a todo titled "Schedule dentist" with:
      | priority | dueDate |
      | High     | +3d     |
    Then I should see "Schedule dentist" in the list
    And I should see "Schedule dentist" with priority "High"
    And it should show a due date within 3 days
