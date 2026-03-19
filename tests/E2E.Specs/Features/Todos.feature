Feature: Todos E2E

  Background:
    Given the app is running at "http://localhost:5173"

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
