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


  # Positive Scenarios

  Scenario: Create a todo with priority and due date
    Given I open the Todos page
    When I create a todo titled "Schedule dentist" with:
      | priority | dueDate |
      | High     | +3d     |
    Then I should see "Schedule dentist" in the list
    And I should see "Schedule dentist" with priority "High"
    And it should show a due date within 3 days

  Scenario: Create an overdue todo
    Given I seed todos:
      | title        | priority | dueDate |
      | Overdue task | High     | -3d     |
    And I open the Todos page
    Then I should see "Overdue task" with priority "High"
    And I should see "Overdue task" marked as overdue

  # Negative Scenarios

  Scenario: Edit title results in duplicate todos error
    Given I seed todos:
      | title        | priority | tags    | dueDate  |
      | update test 1| Low      | inTest  | +10d     |
      | update test 2| Low      | inDev   | +10d     |
    And I open the Todos page
    When I edit "update test 2" to title "update test 1" 
    Then I should see an alert containing the error: "Duplicate title"